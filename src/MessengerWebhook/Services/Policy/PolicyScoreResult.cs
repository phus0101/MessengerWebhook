using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record PolicyScoreResult(
    PolicyAction Action,
    SupportCaseReason Reason,
    string Summary,
    decimal Score,
    decimal Confidence);
