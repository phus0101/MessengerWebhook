using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record PolicyDecision(
    bool RequiresEscalation,
    SupportCaseReason Reason,
    string Summary,
    PolicyAction Action = PolicyAction.Allow,
    decimal Score = 0m,
    decimal Confidence = 0m,
    IReadOnlyList<PolicySignal>? Signals = null,
    bool SemanticClassifierAttempted = false);
