using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IIngredientCompatibilityRepository
{
    Task<List<IngredientCompatibility>> GetCompatibilitiesAsync(string ingredient, CancellationToken cancellationToken = default);
    Task<IngredientCompatibility?> GetByIngredientsAsync(string ingredient1, string ingredient2, CancellationToken cancellationToken = default);
    Task<IngredientCompatibility> CreateAsync(IngredientCompatibility compatibility, CancellationToken cancellationToken = default);
    Task<List<IngredientCompatibility>> GetAllAsync(CancellationToken cancellationToken = default);
}
