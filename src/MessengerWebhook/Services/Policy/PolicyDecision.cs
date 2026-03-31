using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record PolicyDecision(
    bool RequiresEscalation,
    SupportCaseReason Reason,
    string Summary);
