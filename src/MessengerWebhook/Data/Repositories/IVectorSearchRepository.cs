using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IVectorSearchRepository
{
    Task<List<Product>> SearchSimilarProductsAsync(
        float[] queryEmbedding,
        Guid tenantId,
        int limit = 5,
        double similarityThreshold = 0.7,
        CancellationToken cancellationToken = default);

    Task UpdateProductEmbeddingAsync(
        string productId,
        Guid tenantId,
        float[] embedding,
        CancellationToken cancellationToken = default);
}
