namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Represents the status of a Pinecone indexing job
/// </summary>
public enum IndexingStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Tracks progress and state of a Pinecone product indexing job
/// </summary>
public class IndexingJob
{
    public Guid JobId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public IndexingStatus Status { get; set; }
    public int TotalProducts { get; set; }
    public int IndexedProducts { get; set; }
    public string? CurrentProductId { get; set; }
    public string? CurrentProductName { get; set; }
    public string? ErrorMessage { get; set; }

    public int ProgressPercentage => TotalProducts > 0
        ? (int)((double)IndexedProducts / TotalProducts * 100)
        : 0;
}
