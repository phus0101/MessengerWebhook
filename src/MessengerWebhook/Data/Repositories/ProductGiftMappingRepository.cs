using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

/// <summary>
/// Repository implementation for ProductGiftMapping entity
/// </summary>
public class ProductGiftMappingRepository : IProductGiftMappingRepository
{
    private readonly MessengerBotDbContext _context;

    public ProductGiftMappingRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<ProductGiftMapping?> GetByIdAsync(Guid id)
    {
        return await _context.ProductGiftMappings
            .Include(m => m.Product)
            .Include(m => m.Gift)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<ProductGiftMapping>> GetByProductCodeAsync(string productCode)
    {
        return await _context.ProductGiftMappings
            .Include(m => m.Gift)
            .Where(m => m.ProductCode == productCode && m.Gift != null && m.Gift.IsActive)
            .OrderBy(m => m.Priority)
            .ToListAsync();
    }

    public async Task<List<ProductGiftMapping>> GetByGiftCodeAsync(string giftCode)
    {
        return await _context.ProductGiftMappings
            .Include(m => m.Product)
            .Where(m => m.GiftCode == giftCode)
            .ToListAsync();
    }

    public async Task<ProductGiftMapping> CreateAsync(ProductGiftMapping mapping)
    {
        _context.ProductGiftMappings.Add(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task DeleteAsync(Guid id)
    {
        var mapping = await _context.ProductGiftMappings.FindAsync(id);
        if (mapping != null)
        {
            _context.ProductGiftMappings.Remove(mapping);
            await _context.SaveChangesAsync();
        }
    }
}
