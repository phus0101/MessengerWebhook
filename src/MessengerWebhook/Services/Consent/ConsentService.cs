using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.Consent;

/// <summary>
/// PDPL-compliant consent audit service.
/// Records every consent decision and supports withdrawal with PII anonymization.
/// </summary>
public class ConsentService : IConsentService
{
    private readonly MessengerBotDbContext _db;
    private readonly ILogger<ConsentService> _logger;

    public ConsentService(MessengerBotDbContext db, ILogger<ConsentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordConsentAsync(
        Guid tenantId,
        string customerPsid,
        ConsentDecision decision,
        string purpose,
        string channel,
        string consentTextShown,
        CancellationToken ct = default)
    {
        var record = new ConsentAuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPsid = customerPsid,
            Decision = decision,
            Purpose = purpose,
            Channel = channel,
            ConsentTextShown = consentTextShown,
            CreatedAt = DateTime.UtcNow
        };

        _db.ConsentAuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ConsentRecorded Psid={Psid} Decision={Decision} Purpose={Purpose}",
            customerPsid, decision, purpose);
    }

    /// <inheritdoc/>
    public async Task<bool> HasValidConsentAsync(
        Guid tenantId,
        string customerPsid,
        string purpose,
        CancellationToken ct = default)
    {
        // Latest record for this tenant+psid+purpose determines current consent state
        var latest = await _db.ConsentAuditRecords
            .Where(r => r.TenantId == tenantId
                        && r.CustomerPsid == customerPsid
                        && r.Purpose == purpose)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return latest?.Decision is ConsentDecision.Given or ConsentDecision.Implied;
    }

    /// <inheritdoc/>
    public async Task WithdrawConsentAsync(
        Guid tenantId,
        string customerPsid,
        string? reason,
        CancellationToken ct = default)
    {
        // Append withdrawal record — never delete existing audit trail
        var withdrawal = new ConsentAuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPsid = customerPsid,
            Decision = ConsentDecision.Withdrawn,
            Purpose = "all",
            Channel = "admin",
            ConsentTextShown = "",
            CreatedAt = DateTime.UtcNow,
            WithdrawnReason = reason
        };

        _db.ConsentAuditRecords.Add(withdrawal);

        // Anonymize PII on CustomerIdentity rows for this PSID under the same tenant
        var identities = await _db.CustomerIdentities
            .Where(c => c.TenantId == tenantId && c.FacebookPSID == customerPsid)
            .ToListAsync(ct);

        foreach (var identity in identities)
        {
            identity.PhoneNumber = null;
            identity.ShippingAddress = null;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ConsentWithdrawn Psid={Psid} Reason={Reason} IdentitiesAnonymized={Count}",
            customerPsid, reason, identities.Count);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConsentAuditRecord>> GetAuditTrailAsync(
        Guid tenantId,
        string customerPsid,
        CancellationToken ct = default)
    {
        return await _db.ConsentAuditRecords
            .Where(r => r.TenantId == tenantId && r.CustomerPsid == customerPsid)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}
