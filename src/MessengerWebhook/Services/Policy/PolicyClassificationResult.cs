using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record PolicyClassificationResult(
    PolicySemanticIntent Intent,
    decimal Confidence,
    SupportCaseReason Reason,
    string Explanation,
    IReadOnlyList<string> MatchedSpans);
