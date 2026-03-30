using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

/// <summary>
/// Repository interface for ProductGiftMapping entity
/// </summary>
public interface IProductGiftMappingRepository
{
    Task<ProductGiftMapping?> GetByIdAsync(Guid id);
    Task<List<ProductGiftMapping>> GetByProductCodeAsync(string productCode);
    Task<List<ProductGiftMapping>> GetByGiftCodeAsync(string giftCode);
    Task<ProductGiftMapping> CreateAsync(ProductGiftMapping mapping);
    Task DeleteAsync(Guid id);
}
