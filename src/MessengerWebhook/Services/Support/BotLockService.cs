using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class BotLockService : IBotLockService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly SupportOptions _options;

    public BotLockService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        IOptions<SupportOptions> options)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<bool> IsLockedAsync(string facebookPsid, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BotConversationLocks
            .AnyAsync(x => x.FacebookPSID == facebookPsid && x.IsLocked, cancellationToken);
    }

    public async Task LockAsync(
        string facebookPsid,
        string reason,
        Guid? supportCaseId = null,
        CancellationToken cancellationToken = default)
    {
        var existingLock = await _dbContext.BotConversationLocks
            .FirstOrDefaultAsync(x => x.FacebookPSID == facebookPsid && x.IsLocked, cancellationToken);
        var unlockAt = DateTime.UtcNow.AddMinutes(_options.BotLockTimeoutMinutes);

        if (existingLock != null)
        {
            existingLock.Reason = reason;
            existingLock.HumanSupportCaseId = supportCaseId;
            existingLock.UnlockAt = unlockAt;
        }
        else
        {
            _dbContext.BotConversationLocks.Add(new BotConversationLock
            {
                TenantId = _tenantContext.TenantId,
                FacebookPSID = facebookPsid,
                Reason = reason,
                HumanSupportCaseId = supportCaseId,
                UnlockAt = unlockAt
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string facebookPsid, CancellationToken cancellationToken = default)
    {
        var activeLocks = await _dbContext.BotConversationLocks
            .Where(x => x.FacebookPSID == facebookPsid && x.IsLocked)
            .ToListAsync(cancellationToken);

        foreach (var activeLock in activeLocks)
        {
            activeLock.IsLocked = false;
            activeLock.ReleasedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
