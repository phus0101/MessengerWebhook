using System.Collections.Concurrent;

namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// In-memory progress tracker for Pinecone indexing jobs with TTL cleanup
/// </summary>
public class IndexingProgressTracker : IIndexingProgressTracker, IDisposable
{
    private readonly ConcurrentDictionary<Guid, IndexingJob> _jobs = new();
    private readonly ILogger<IndexingProgressTracker> _logger;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _jobCreationLock = new(1, 1);
    private const int MaxJobs = 100;
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(1);

    public IndexingProgressTracker(ILogger<IndexingProgressTracker> logger)
    {
        _logger = logger;

        // Cleanup expired jobs every 5 minutes
        _cleanupTimer = new Timer(
            CleanupExpiredJobs,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    public Guid CreateJob(int totalProducts)
    {
        // Atomic check-and-create to prevent race condition
        _jobCreationLock.Wait();
        try
        {
            // Check for active jobs
            var activeJobs = _jobs.Values.Where(j => j.Status == IndexingStatus.Running).ToList();
            if (activeJobs.Count > 0)
            {
                throw new InvalidOperationException($"An indexing job is already running: {activeJobs[0].JobId}");
            }

            var jobId = Guid.NewGuid();
            var job = new IndexingJob
            {
                JobId = jobId,
                StartedAt = DateTime.UtcNow,
                Status = IndexingStatus.Running,
                TotalProducts = totalProducts,
                IndexedProducts = 0
            };

            // Enforce max capacity
            if (_jobs.Count >= MaxJobs)
            {
                RemoveOldestCompletedJob();
            }

            _jobs[jobId] = job;
            _logger.LogInformation("Created indexing job {JobId} for {TotalProducts} products", jobId, totalProducts);

            return jobId;
        }
        finally
        {
            _jobCreationLock.Release();
        }
    }

    public void UpdateProgress(Guid jobId, int indexedCount, string? currentProductId, string? currentProductName)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.IndexedProducts = indexedCount;
            job.CurrentProductId = currentProductId;
            job.CurrentProductName = currentProductName;
        }
    }

    public void CompleteJob(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = IndexingStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.CurrentProductId = null;
            job.CurrentProductName = null;

            _logger.LogInformation(
                "Completed indexing job {JobId}: {IndexedProducts}/{TotalProducts} products",
                jobId, job.IndexedProducts, job.TotalProducts);
        }
    }

    public void FailJob(Guid jobId, string errorMessage)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = IndexingStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = errorMessage;
            job.CurrentProductId = null;
            job.CurrentProductName = null;

            _logger.LogError("Failed indexing job {JobId}: {ErrorMessage}", jobId, errorMessage);
        }
    }

    public IndexingJob? GetJob(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    public IReadOnlyList<IndexingJob> GetActiveJobs()
    {
        return _jobs.Values
            .Where(j => j.Status == IndexingStatus.Running)
            .ToList();
    }

    private void CleanupExpiredJobs(object? state)
    {
        var now = DateTime.UtcNow;

        // Cleanup completed/failed jobs older than TTL
        var expiredJobs = _jobs.Values
            .Where(j => j.CompletedAt.HasValue && now - j.CompletedAt.Value > JobTtl)
            .Select(j => j.JobId)
            .ToList();

        // Cleanup stuck running jobs (running for more than 2 hours)
        var stuckJobs = _jobs.Values
            .Where(j => j.Status == IndexingStatus.Running && now - j.StartedAt > TimeSpan.FromHours(2))
            .Select(j => j.JobId)
            .ToList();

        foreach (var jobId in expiredJobs)
        {
            if (_jobs.TryRemove(jobId, out _))
            {
                _logger.LogDebug("Removed expired job {JobId}", jobId);
            }
        }

        foreach (var jobId in stuckJobs)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = IndexingStatus.Failed;
                job.CompletedAt = now;
                job.ErrorMessage = "Job timeout - exceeded 2 hour limit";
                _logger.LogWarning("Marked stuck job {JobId} as failed after 2 hours", jobId);
            }
        }

        var totalCleaned = expiredJobs.Count + stuckJobs.Count;
        if (totalCleaned > 0)
        {
            _logger.LogInformation("Cleaned up {ExpiredCount} expired and {StuckCount} stuck indexing jobs",
                expiredJobs.Count, stuckJobs.Count);
        }
    }

    private void RemoveOldestCompletedJob()
    {
        var oldestCompleted = _jobs.Values
            .Where(j => j.CompletedAt.HasValue)
            .OrderBy(j => j.CompletedAt)
            .FirstOrDefault();

        if (oldestCompleted != null && _jobs.TryRemove(oldestCompleted.JobId, out _))
        {
            _logger.LogWarning(
                "Removed oldest completed job {JobId} to enforce max capacity {MaxJobs}",
                oldestCompleted.JobId, MaxJobs);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _jobCreationLock?.Dispose();
    }
}
