using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetByCategoryAsync(ProductCategory category);
    Task<Product?> GetByIdAsync(string id);
    Task<Product?> GetActiveByIdAsync(string id, Guid tenantId);
    Task<Product?> GetActiveByCodeAsync(string code, Guid tenantId);
    Task<List<Product>> GetActiveRelatedAsync(Guid tenantId, ProductCategory? category, IReadOnlyCollection<string> normalizedTerms, int maxCount, CancellationToken cancellationToken = default);
    Task<Product?> GetByCodeAsync(string code);
    Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId);
}
