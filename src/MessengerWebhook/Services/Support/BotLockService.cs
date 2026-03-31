using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class BotLockService : IBotLockService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAdminAuditService _auditService;
    private readonly SupportOptions _options;
    private readonly ILogger<BotLockService> _logger;

    public BotLockService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        IAdminAuditService auditService,
        IOptions<SupportOptions> options,
        ILogger<BotLockService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _auditService = auditService;
        _options = options.Value;
        _logger = logger;
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
                FacebookPageId = _tenantContext.FacebookPageId,
                Reason = reason,
                HumanSupportCaseId = supportCaseId,
                UnlockAt = unlockAt
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Audit log
        try
        {
            var systemActor = new AdminUserContext(
                Guid.Empty,
                _tenantContext.TenantId ?? Guid.Empty,
                "system",
                "System",
                _tenantContext.FacebookPageId);

            await _auditService.LogAsync(
                systemActor,
                "bot_lock",
                "BotConversationLock",
                facebookPsid,
                $"Locked bot for PSID {facebookPsid}. Reason: {reason}. Unlock at: {unlockAt:yyyy-MM-dd HH:mm:ss}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log bot lock audit for PSID {PSID}", facebookPsid);
        }
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

        // Audit log
        if (activeLocks.Count > 0)
        {
            try
            {
                var systemActor = new AdminUserContext(
                    Guid.Empty,
                    _tenantContext.TenantId ?? Guid.Empty,
                    "system",
                    "System",
                    _tenantContext.FacebookPageId);

                await _auditService.LogAsync(
                    systemActor,
                    "bot_unlock",
                    "BotConversationLock",
                    facebookPsid,
                    $"Unlocked bot for PSID {facebookPsid}. Released {activeLocks.Count} lock(s).",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log bot unlock audit for PSID {PSID}", facebookPsid);
            }
        }
    }

    public async Task ExtendLockAsync(
        string facebookPsid,
        int additionalMinutes,
        CancellationToken cancellationToken = default)
    {
        var activeLock = await _dbContext.BotConversationLocks
            .FirstOrDefaultAsync(x => x.FacebookPSID == facebookPsid && x.IsLocked, cancellationToken);

        if (activeLock == null)
        {
            throw new InvalidOperationException($"No active lock found for PSID {facebookPsid}");
        }

        activeLock.UnlockAt = activeLock.UnlockAt?.AddMinutes(additionalMinutes) ?? DateTime.UtcNow.AddMinutes(additionalMinutes);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<BotConversationLock>> GetActiveLocksAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BotConversationLocks
            .Where(x => x.IsLocked)
            .OrderByDescending(x => x.LockedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BotConversationLock>> GetLockHistoryAsync(
        string facebookPsid,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.BotConversationLocks
            .Where(x => x.FacebookPSID == facebookPsid)
            .OrderByDescending(x => x.LockedAt)
            .ToListAsync(cancellationToken);
    }
}
