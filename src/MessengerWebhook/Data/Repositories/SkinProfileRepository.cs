using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class SkinProfileRepository : ISkinProfileRepository
{
    private readonly MessengerBotDbContext _context;

    public SkinProfileRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<SkinProfile?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.SkinProfiles
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
    }

    public async Task<SkinProfile> CreateAsync(SkinProfile skinProfile, CancellationToken cancellationToken = default)
    {
        // TODO: Phase 7 - Add FK validation: check SessionId exists before insert
        _context.SkinProfiles.Add(skinProfile);
        await _context.SaveChangesAsync(cancellationToken);
        return skinProfile;
    }

    public async Task UpdateAsync(SkinProfile skinProfile, CancellationToken cancellationToken = default)
    {
        skinProfile.UpdatedAt = DateTime.UtcNow;
        _context.SkinProfiles.Update(skinProfile);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var skinProfile = await _context.SkinProfiles.FindAsync(new object[] { id }, cancellationToken);
        if (skinProfile != null)
        {
            _context.SkinProfiles.Remove(skinProfile);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
