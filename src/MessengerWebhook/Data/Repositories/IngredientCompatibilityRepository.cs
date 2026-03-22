using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class IngredientCompatibilityRepository : IIngredientCompatibilityRepository
{
    private readonly MessengerBotDbContext _context;

    public IngredientCompatibilityRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<List<IngredientCompatibility>> GetCompatibilitiesAsync(string ingredient, CancellationToken cancellationToken = default)
    {
        return await _context.IngredientCompatibilities
            .Where(c => c.Ingredient1 == ingredient || c.Ingredient2 == ingredient)
            .ToListAsync(cancellationToken);
    }

    public async Task<IngredientCompatibility?> GetByIngredientsAsync(string ingredient1, string ingredient2, CancellationToken cancellationToken = default)
    {
        return await _context.IngredientCompatibilities
            .FirstOrDefaultAsync(c =>
                (c.Ingredient1 == ingredient1 && c.Ingredient2 == ingredient2) ||
                (c.Ingredient1 == ingredient2 && c.Ingredient2 == ingredient1),
                cancellationToken);
    }

    public async Task<IngredientCompatibility> CreateAsync(IngredientCompatibility compatibility, CancellationToken cancellationToken = default)
    {
        _context.IngredientCompatibilities.Add(compatibility);
        await _context.SaveChangesAsync(cancellationToken);
        return compatibility;
    }

    public async Task<List<IngredientCompatibility>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IngredientCompatibilities.ToListAsync(cancellationToken);
    }
}
