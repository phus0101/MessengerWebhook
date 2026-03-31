using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class CaseEscalationService : ICaseEscalationService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IBotLockService _botLockService;
    private readonly SupportOptions _options;
    private readonly ILogger<CaseEscalationService> _logger;

    public CaseEscalationService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        IBotLockService botLockService,
        IOptions<SupportOptions> options,
        ILogger<CaseEscalationService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _botLockService = botLockService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HumanSupportCase> EscalateAsync(
        string facebookPsid,
        SupportCaseReason reason,
        string summary,
        string transcriptExcerpt,
        Guid? draftOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var supportCase = new HumanSupportCase
        {
            TenantId = _tenantContext.TenantId,
            FacebookPSID = facebookPsid,
            DraftOrderId = draftOrderId,
            Reason = reason,
            Summary = summary,
            TranscriptExcerpt = transcriptExcerpt,
            AssignedToEmail = _tenantContext.ManagerEmail ?? _options.DefaultManagerEmail
        };

        _dbContext.HumanSupportCases.Add(supportCase);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _botLockService.LockAsync(facebookPsid, summary, supportCase.Id, cancellationToken);

        _logger.LogWarning(
            "Created support case {CaseId} for {PSID}. AssignedTo: {Email}",
            supportCase.Id,
            facebookPsid,
            supportCase.AssignedToEmail);

        return supportCase;
    }
}
