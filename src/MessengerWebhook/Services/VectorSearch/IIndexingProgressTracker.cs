namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Tracks progress of Pinecone indexing jobs in memory
/// </summary>
public interface IIndexingProgressTracker
{
    /// <summary>
    /// Creates a new indexing job
    /// </summary>
    Guid CreateJob(int totalProducts);

    /// <summary>
    /// Atomically creates a new job only when no running job exists.
    /// Returns null when another job is already running.
    /// </summary>
    Guid? TryCreateJob(int totalProducts);

    /// <summary>
    /// Updates progress for an active job
    /// </summary>
    void UpdateProgress(Guid jobId, int indexedCount, string? currentProductId, string? currentProductName);

    /// <summary>
    /// Marks job as completed successfully
    /// </summary>
    void CompleteJob(Guid jobId);

    /// <summary>
    /// Marks job as failed with error message
    /// </summary>
    void FailJob(Guid jobId, string errorMessage);

    /// <summary>
    /// Gets job by ID, returns null if not found or expired
    /// </summary>
    IndexingJob? GetJob(Guid jobId);

    /// <summary>
    /// Gets all active (running) jobs
    /// </summary>
    IReadOnlyList<IndexingJob> GetActiveJobs();

    /// <summary>
    /// Clears tracked jobs. Intended for test/reset scenarios.
    /// </summary>
    void Reset();
}
