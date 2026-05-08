using MessengerWebhook.Services.ProductGrounding;

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
        bool includeDetailedInfo = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG context with formatted text and metadata
/// </summary>
public record RAGContext(
    string FormattedContext,
    List<string> ProductIds,
    List<GroundedProduct> Products,
    RAGMetrics Metrics);

public record AssembledRAGContext(
    string FormattedContext,
    List<string> ProductIds,
    List<GroundedProduct> Products);

/// <summary>
/// Performance metrics for RAG retrieval
/// </summary>
public record RAGMetrics(
    TimeSpan RetrievalLatency,
    TimeSpan TotalLatency,
    int ProductsRetrieved,
    bool CacheHit,
    string Source); // "hybrid", "vector-only", "fallback"
