using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class ConversationMessageRepository : IConversationMessageRepository
{
    private readonly MessengerBotDbContext _context;

    public ConversationMessageRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<List<ConversationMessage>> GetBySessionIdAsync(string sessionId, int limit = 10, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationMessage> CreateAsync(ConversationMessage message, CancellationToken cancellationToken = default)
    {
        // TODO: Phase 7 - Add FK validation: check SessionId exists before insert
        _context.ConversationMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldMessages = await _context.ConversationMessages
            .Where(m => m.CreatedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.ConversationMessages.RemoveRange(oldMessages);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
