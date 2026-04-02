namespace MessengerWebhook.Services.RAG;

/// <summary>
/// Service for Retrieval-Augmented Generation (RAG) operations
/// </summary>
public interface IRAGService
{
    /// <summary>
    /// Retrieve relevant products and assemble context for LLM
    /// </summary>
    Task<RAGContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG context with formatted text and metadata
/// </summary>
public record RAGContext(
    string FormattedContext,
    List<string> ProductIds,
    RAGMetrics Metrics);

/// <summary>
/// Performance metrics for RAG retrieval
/// </summary>
public record RAGMetrics(
    TimeSpan RetrievalLatency,
    TimeSpan TotalLatency,
    int ProductsRetrieved,
    bool CacheHit,
    string Source); // "hybrid", "vector-only", "fallback"
