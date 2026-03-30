using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

/// <summary>
/// Repository interface for Gift entity
/// </summary>
public interface IGiftRepository
{
    Task<Gift?> GetByIdAsync(Guid id);
    Task<Gift?> GetByCodeAsync(string code);
    Task<List<Gift>> GetAllActiveAsync();
    Task<Gift> CreateAsync(Gift gift);
    Task<Gift> UpdateAsync(Gift gift);
    Task DeleteAsync(Guid id);
}
