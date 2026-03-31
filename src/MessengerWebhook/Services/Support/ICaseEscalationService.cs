using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Support;

public interface ICaseEscalationService
{
    Task<HumanSupportCase> EscalateAsync(
        string facebookPsid,
        SupportCaseReason reason,
        string summary,
        string transcriptExcerpt,
        Guid? draftOrderId = null,
        CancellationToken cancellationToken = default);
}
