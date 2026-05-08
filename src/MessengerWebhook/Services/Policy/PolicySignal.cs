using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Policy;

public sealed record PolicySignal(
    string Detector,
    string Category,
    string MatchedText,
    decimal Weight,
    decimal Confidence,
    SupportCaseReason Reason,
    string Summary);
