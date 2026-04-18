using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.VectorSearch;

public class IndexingProgressTrackerTests : IDisposable
{
    private readonly IndexingProgressTracker _tracker;
    private readonly Mock<ILogger<IndexingProgressTracker>> _loggerMock;

    public IndexingProgressTrackerTests()
    {
        _loggerMock = new Mock<ILogger<IndexingProgressTracker>>();
        _tracker = new IndexingProgressTracker(_loggerMock.Object);
    }

    public void Dispose()
    {
        _tracker?.Dispose();
    }

    [Fact]
    public void CreateJob_ShouldCreateJobWithRunningStatus()
    {
        // Arrange
        var totalProducts = 100;

        // Act
        var jobId = _tracker.CreateJob(totalProducts);

        // Assert
        var job = _tracker.GetJob(jobId);
        Assert.NotNull(job);
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(IndexingStatus.Running, job.Status);
        Assert.Equal(totalProducts, job.TotalProducts);
        Assert.Equal(0, job.IndexedProducts);
        Assert.Null(job.CompletedAt);
    }

    [Fact]
    public void UpdateProgress_ShouldUpdateJobProgress()
    {
        // Arrange
        var jobId = _tracker.CreateJob(100);

        // Act
        _tracker.UpdateProgress(jobId, 50, "prod-123", "Test Product");

        // Assert
        var job = _tracker.GetJob(jobId);
        Assert.NotNull(job);
        Assert.Equal(50, job.IndexedProducts);
        Assert.Equal("prod-123", job.CurrentProductId);
        Assert.Equal("Test Product", job.CurrentProductName);
        Assert.Equal(50, job.ProgressPercentage);
    }

    [Fact]
    public void CompleteJob_ShouldMarkJobAsCompleted()
    {
        // Arrange
        var jobId = _tracker.CreateJob(100);
        _tracker.UpdateProgress(jobId, 100, "prod-100", "Last Product");

        // Act
        _tracker.CompleteJob(jobId);

        // Assert
        var job = _tracker.GetJob(jobId);
        Assert.NotNull(job);
        Assert.Equal(IndexingStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.CurrentProductId);
        Assert.Null(job.CurrentProductName);
    }

    [Fact]
    public void FailJob_ShouldMarkJobAsFailed()
    {
        // Arrange
        var jobId = _tracker.CreateJob(100);
        var errorMessage = "Connection timeout";

        // Act
        _tracker.FailJob(jobId, errorMessage);

        // Assert
        var job = _tracker.GetJob(jobId);
        Assert.NotNull(job);
        Assert.Equal(IndexingStatus.Failed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Equal(errorMessage, job.ErrorMessage);
        Assert.Null(job.CurrentProductId);
        Assert.Null(job.CurrentProductName);
    }

    [Fact]
    public void GetJob_ShouldReturnNullForNonExistentJob()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var job = _tracker.GetJob(nonExistentJobId);

        // Assert
        Assert.Null(job);
    }

    [Fact]
    public void GetActiveJobs_ShouldReturnOnlyRunningJobs()
    {
        // Arrange
        var runningJob1 = _tracker.CreateJob(100);
        var runningJob2 = _tracker.CreateJob(200);
        var completedJob = _tracker.CreateJob(50);
        var failedJob = _tracker.CreateJob(75);

        _tracker.CompleteJob(completedJob);
        _tracker.FailJob(failedJob, "Error");

        // Act
        var activeJobs = _tracker.GetActiveJobs();

        // Assert
        Assert.Equal(2, activeJobs.Count);
        Assert.Contains(activeJobs, j => j.JobId == runningJob1);
        Assert.Contains(activeJobs, j => j.JobId == runningJob2);
        Assert.DoesNotContain(activeJobs, j => j.JobId == completedJob);
        Assert.DoesNotContain(activeJobs, j => j.JobId == failedJob);
    }

    [Fact]
    public void TryCreateJob_ShouldReturnNull_WhenAnotherJobIsRunning()
    {
        _tracker.CreateJob(10);

        var secondJobId = _tracker.TryCreateJob(20);

        Assert.Null(secondJobId);
    }

    [Fact]
    public void TryCreateJob_ShouldCreateNewJob_AfterRunningJobCompletes()
    {
        var firstJobId = _tracker.CreateJob(10);
        _tracker.CompleteJob(firstJobId);

        var secondJobId = _tracker.TryCreateJob(20);

        Assert.NotNull(secondJobId);
        Assert.NotEqual(Guid.Empty, secondJobId.Value);
        Assert.Equal(IndexingStatus.Running, _tracker.GetJob(secondJobId.Value)!.Status);
    }

    [Fact]
    public void ProgressPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var jobId = _tracker.CreateJob(100);

        // Act & Assert
        _tracker.UpdateProgress(jobId, 0, null, null);
        Assert.Equal(0, _tracker.GetJob(jobId)!.ProgressPercentage);

        _tracker.UpdateProgress(jobId, 25, null, null);
        Assert.Equal(25, _tracker.GetJob(jobId)!.ProgressPercentage);

        _tracker.UpdateProgress(jobId, 50, null, null);
        Assert.Equal(50, _tracker.GetJob(jobId)!.ProgressPercentage);

        _tracker.UpdateProgress(jobId, 100, null, null);
        Assert.Equal(100, _tracker.GetJob(jobId)!.ProgressPercentage);
    }

    [Fact]
    public void ProgressPercentage_ShouldReturnZeroForZeroTotalProducts()
    {
        // Arrange
        var jobId = _tracker.CreateJob(0);

        // Act
        var job = _tracker.GetJob(jobId);

        // Assert
        Assert.NotNull(job);
        Assert.Equal(0, job.ProgressPercentage);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var jobId = _tracker.CreateJob(1000);
        var tasks = new List<Task>();

        // Act - Simulate concurrent progress updates
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                _tracker.UpdateProgress(jobId, index, $"prod-{index}", $"Product {index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Job should still be accessible and valid
        var job = _tracker.GetJob(jobId);
        Assert.NotNull(job);
        Assert.Equal(IndexingStatus.Running, job.Status);
    }

    [Fact]
    public void CreateJob_ShouldEnforceMaxCapacity()
    {
        // Arrange - Create 100 jobs (max capacity)
        var jobIds = new List<Guid>();
        for (int i = 0; i < 100; i++)
        {
            var jobId = _tracker.CreateJob(10);
            jobIds.Add(jobId);
            if (i < 50)
            {
                _tracker.CompleteJob(jobId); // Complete first 50
            }
        }

        // Act - Create one more job (should trigger cleanup)
        var newJobId = _tracker.CreateJob(10);

        // Assert - New job should exist
        Assert.NotNull(_tracker.GetJob(newJobId));

        // At least one old completed job should be removed
        var remainingJobs = jobIds.Count(id => _tracker.GetJob(id) != null);
        Assert.True(remainingJobs < 100);
    }

    [Fact]
    public void UpdateProgress_ShouldHandleNonExistentJob()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act - Should not throw
        _tracker.UpdateProgress(nonExistentJobId, 50, "prod-123", "Test");

        // Assert
        Assert.Null(_tracker.GetJob(nonExistentJobId));
    }

    [Fact]
    public void CompleteJob_ShouldHandleNonExistentJob()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act - Should not throw
        _tracker.CompleteJob(nonExistentJobId);

        // Assert
        Assert.Null(_tracker.GetJob(nonExistentJobId));
    }

    [Fact]
    public void FailJob_ShouldHandleNonExistentJob()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act - Should not throw
        _tracker.FailJob(nonExistentJobId, "Error");

        // Assert
        Assert.Null(_tracker.GetJob(nonExistentJobId));
    }

    [Fact]
    public void MultipleJobs_ShouldBeIndependent()
    {
        // Arrange
        var job1 = _tracker.CreateJob(100);
        var job2 = _tracker.CreateJob(200);

        // Act
        _tracker.UpdateProgress(job1, 50, "prod-1", "Product 1");
        _tracker.UpdateProgress(job2, 100, "prod-2", "Product 2");
        _tracker.CompleteJob(job1);

        // Assert
        var retrievedJob1 = _tracker.GetJob(job1);
        var retrievedJob2 = _tracker.GetJob(job2);

        Assert.NotNull(retrievedJob1);
        Assert.NotNull(retrievedJob2);
        Assert.Equal(IndexingStatus.Completed, retrievedJob1.Status);
        Assert.Equal(IndexingStatus.Running, retrievedJob2.Status);
        Assert.Equal(50, retrievedJob1.IndexedProducts);
        Assert.Equal(100, retrievedJob2.IndexedProducts);
    }
}
