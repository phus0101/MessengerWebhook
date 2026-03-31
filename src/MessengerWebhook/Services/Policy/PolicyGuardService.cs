using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public class PolicyGuardService : IPolicyGuardService
{
    private static readonly (string Keyword, SupportCaseReason Reason)[] BuiltInKeywords =
    {
        ("huy don", SupportCaseReason.CancellationRequest),
        ("hoan tien", SupportCaseReason.RefundRequest),
        ("refund", SupportCaseReason.RefundRequest),
        ("prompt injection", SupportCaseReason.PromptInjection),
        ("mien phi van chuyen", SupportCaseReason.PolicyException),
        ("them khuyen mai", SupportCaseReason.PolicyException),
        ("giam gia them", SupportCaseReason.PolicyException),
        ("nhan vien ho tro", SupportCaseReason.ManualReview)
    };

    private readonly SalesBotOptions _options;
    private readonly HashSet<string> _keywords;

    public PolicyGuardService(IOptions<SalesBotOptions> options)
    {
        _options = options.Value;
        _keywords = _options.EscalationKeywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(BuiltInKeywords.Select(x => x.Keyword))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public PolicyDecision Evaluate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new PolicyDecision(false, SupportCaseReason.ManualReview, string.Empty);
        }

        foreach (var keyword in _keywords)
        {
            if (!message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reason = BuiltInKeywords
                .FirstOrDefault(x => keyword.Contains(x.Keyword, StringComparison.OrdinalIgnoreCase))
                .Reason;
            if (reason == 0 && !keyword.Contains("refund", StringComparison.OrdinalIgnoreCase))
            {
                reason = SupportCaseReason.PolicyException;
            }

            return new PolicyDecision(true, reason, $"Escalated by keyword: {keyword}");
        }

        return new PolicyDecision(false, SupportCaseReason.ManualReview, string.Empty);
    }

    public string EnsureClosingCallToAction(string response)
    {
        var safeResponse = string.IsNullOrWhiteSpace(response)
            ? "Da em ho tro chi ngay day a."
            : response.Trim();

        if (safeResponse.Contains(_options.ClosingCallToAction, StringComparison.OrdinalIgnoreCase))
        {
            return safeResponse;
        }

        return $"{safeResponse}\n\n{_options.ClosingCallToAction}";
    }
}
