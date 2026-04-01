namespace MessengerWebhook.Services.AI.Embeddings;

/// <summary>
/// Service for generating text embeddings using Vertex AI
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding for single text
    /// </summary>
    Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default);
}
