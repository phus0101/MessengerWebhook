using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly MessengerBotDbContext _context;

    public ProductRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetByCategoryAsync(ProductCategory category)
    {
        return await _context.Products
            .Where(p => p.Category == category && p.IsActive)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId)
    {
        return await _context.ProductVariants
            .Where(v => v.ProductId == productId && v.IsAvailable)
            .ToListAsync();
    }
}
