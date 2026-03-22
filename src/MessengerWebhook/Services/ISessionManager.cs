using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services;

public interface ISessionManager
{
    Task<ConversationSession?> GetAsync(string psid);
    Task SaveAsync(ConversationSession session);
    Task DeleteAsync(string psid);
}
