namespace MessengerWebhook.Services.VectorSearch;

public interface IVectorSearchService
{
    Task<UpsertResult> UpsertProductAsync(
        string productId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    Task<UpsertResult> UpsertBatchAsync(
        List<(string productId, float[] embedding, Dictionary<string, object> metadata)> products,
        CancellationToken cancellationToken = default);

    Task<List<ProductSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    Task DeleteProductAsync(
        string productId,
        CancellationToken cancellationToken = default);

    Task DeleteBatchAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default);
}

public class UpsertResult
{
    public int UpsertedCount { get; set; }
    public List<string> FailedIds { get; set; } = new();
}

public class ProductSearchResult
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public float Score { get; set; }
}
