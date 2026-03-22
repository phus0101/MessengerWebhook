using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Data.Repositories;

public interface IConversationMessageRepository
{
    Task<List<ConversationMessage>> GetBySessionIdAsync(string sessionId, int limit = 10, CancellationToken cancellationToken = default);
    Task<ConversationMessage> CreateAsync(ConversationMessage message, CancellationToken cancellationToken = default);
    Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
