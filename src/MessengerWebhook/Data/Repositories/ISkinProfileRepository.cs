using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface ISkinProfileRepository
{
    Task<SkinProfile?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<SkinProfile> CreateAsync(SkinProfile skinProfile, CancellationToken cancellationToken = default);
    Task UpdateAsync(SkinProfile skinProfile, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
