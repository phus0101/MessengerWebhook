using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetByCategoryAsync(ProductCategory category);
    Task<Product?> GetByIdAsync(string id);
    Task<Product?> GetByCodeAsync(string code);
    Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId);
}
