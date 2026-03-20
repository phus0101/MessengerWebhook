using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface ISessionRepository
{
    Task<ConversationSession?> GetByPSIDAsync(string psid);
    Task<ConversationSession> CreateAsync(ConversationSession session);
    Task UpdateAsync(ConversationSession session);
    Task DeleteExpiredSessionsAsync();
}
