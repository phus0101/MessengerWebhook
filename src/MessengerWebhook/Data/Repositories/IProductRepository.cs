using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetByCategoryAsync(string category);
    Task<Product?> GetByIdAsync(string id);
    Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId);
}
