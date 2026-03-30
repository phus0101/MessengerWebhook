using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

/// <summary>
/// Repository implementation for Gift entity
/// </summary>
public class GiftRepository : IGiftRepository
{
    private readonly MessengerBotDbContext _context;

    public GiftRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<Gift?> GetByIdAsync(Guid id)
    {
        return await _context.Gifts.FindAsync(id);
    }

    public async Task<Gift?> GetByCodeAsync(string code)
    {
        return await _context.Gifts
            .FirstOrDefaultAsync(g => g.Code == code);
    }

    public async Task<List<Gift>> GetAllActiveAsync()
    {
        return await _context.Gifts
            .Where(g => g.IsActive)
            .ToListAsync();
    }

    public async Task<Gift> CreateAsync(Gift gift)
    {
        _context.Gifts.Add(gift);
        await _context.SaveChangesAsync();
        return gift;
    }

    public async Task<Gift> UpdateAsync(Gift gift)
    {
        gift.UpdatedAt = DateTime.UtcNow;
        _context.Gifts.Update(gift);
        await _context.SaveChangesAsync();
        return gift;
    }

    public async Task DeleteAsync(Guid id)
    {
        var gift = await GetByIdAsync(id);
        if (gift != null)
        {
            _context.Gifts.Remove(gift);
            await _context.SaveChangesAsync();
        }
    }
}
