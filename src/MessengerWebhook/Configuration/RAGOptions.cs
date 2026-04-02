namespace MessengerWebhook.Configuration;

/// <summary>
/// Configuration options for RAG (Retrieval-Augmented Generation)
/// </summary>
public class RAGOptions
{
    public const string SectionName = "RAG";

    /// <summary>
    /// Enable/disable RAG feature (feature flag for gradual rollout)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Number of top products to retrieve
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Fallback strategy when RAG fails
    /// </summary>
    public string FallbackStrategy { get; set; } = "full-context";

    /// <summary>
    /// Timeout for RAG retrieval in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
