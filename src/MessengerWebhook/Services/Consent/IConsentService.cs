using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Consent;

public interface IConsentService
{
    Task RecordConsentAsync(
        Guid tenantId,
        string customerPsid,
        ConsentDecision decision,
        string purpose,
        string channel,
        string consentTextShown,
        CancellationToken ct = default);

    Task<bool> HasValidConsentAsync(
        Guid tenantId,
        string customerPsid,
        string purpose,
        CancellationToken ct = default);

    Task WithdrawConsentAsync(
        Guid tenantId,
        string customerPsid,
        string? reason,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConsentAuditRecord>> GetAuditTrailAsync(
        Guid tenantId,
        string customerPsid,
        CancellationToken ct = default);
}
