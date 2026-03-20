using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly MessengerBotDbContext _context;

    public SessionRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<ConversationSession?> GetByPSIDAsync(string psid)
    {
        return await _context.ConversationSessions
            .FirstOrDefaultAsync(s => s.FacebookPSID == psid);
    }

    public async Task<ConversationSession> CreateAsync(ConversationSession session)
    {
        _context.ConversationSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task UpdateAsync(ConversationSession session)
    {
        session.LastActivityAt = DateTime.UtcNow;
        _context.ConversationSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExpiredSessionsAsync()
    {
        var expiredSessions = await _context.ConversationSessions
            .Where(s => s.ExpiresAt != null && s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        _context.ConversationSessions.RemoveRange(expiredSessions);
        await _context.SaveChangesAsync();
    }
}
