using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Admin;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.Support;

public class SupportCaseManagementService : ISupportCaseManagementService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IBotLockService _botLockService;
    private readonly IAdminAuditService _adminAuditService;

    public SupportCaseManagementService(
        MessengerBotDbContext dbContext,
        IBotLockService botLockService,
        IAdminAuditService adminAuditService)
    {
        _dbContext = dbContext;
        _botLockService = botLockService;
        _adminAuditService = adminAuditService;
    }

    public async Task<HumanSupportCase?> ClaimAsync(AdminUserContext actor, Guid caseId, CancellationToken cancellationToken = default)
    {
        var supportCase = await FindCaseAsync(actor, caseId, cancellationToken);
        if (supportCase == null)
        {
            return null;
        }

        supportCase.Status = SupportCaseStatus.Claimed;
        supportCase.ClaimedAt ??= DateTime.UtcNow;
        supportCase.ClaimedByEmail = actor.Email;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminAuditService.LogAsync(actor, "claim", "support-case", supportCase.Id.ToString(), supportCase.Summary, cancellationToken);
        return supportCase;
    }

    public async Task<HumanSupportCase?> ResolveAsync(AdminUserContext actor, Guid caseId, string? resolutionNotes, CancellationToken cancellationToken = default)
    {
        var supportCase = await FindCaseAsync(actor, caseId, cancellationToken);
        if (supportCase == null)
        {
            return null;
        }

        supportCase.Status = SupportCaseStatus.Resolved;
        supportCase.ResolutionNotes = resolutionNotes;
        supportCase.ResolvedAt = DateTime.UtcNow;
        supportCase.ResolvedByEmail = actor.Email;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _botLockService.ReleaseAsync(supportCase.FacebookPSID, cancellationToken);
        await _adminAuditService.LogAsync(actor, "resolve", "support-case", supportCase.Id.ToString(), resolutionNotes, cancellationToken);
        return supportCase;
    }

    public async Task<HumanSupportCase?> CancelAsync(AdminUserContext actor, Guid caseId, string? resolutionNotes, CancellationToken cancellationToken = default)
    {
        var supportCase = await FindCaseAsync(actor, caseId, cancellationToken);
        if (supportCase == null)
        {
            return null;
        }

        supportCase.Status = SupportCaseStatus.Cancelled;
        supportCase.ResolutionNotes = resolutionNotes;
        supportCase.ResolvedAt = DateTime.UtcNow;
        supportCase.ResolvedByEmail = actor.Email;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _botLockService.ReleaseAsync(supportCase.FacebookPSID, cancellationToken);
        await _adminAuditService.LogAsync(actor, "cancel", "support-case", supportCase.Id.ToString(), resolutionNotes, cancellationToken);
        return supportCase;
    }

    private Task<HumanSupportCase?> FindCaseAsync(AdminUserContext actor, Guid caseId, CancellationToken cancellationToken)
    {
        return _dbContext.HumanSupportCases.FirstOrDefaultAsync(
            x => x.Id == caseId &&
                 x.TenantId == actor.TenantId &&
                 (actor.FacebookPageId == null || x.FacebookPageId == actor.FacebookPageId),
            cancellationToken);
    }
}
