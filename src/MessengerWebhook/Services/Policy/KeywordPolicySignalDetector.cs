using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public sealed class KeywordPolicySignalDetector : IPolicySignalDetector
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

    private readonly (string Keyword, SupportCaseReason Reason)[] _keywords;

    public KeywordPolicySignalDetector(
        IOptions<PolicyGuardOptions> policyOptions,
        IPolicyMessageNormalizer normalizer)
    {
        _keywords = policyOptions.Value.EscalationKeywords
            .Select(keyword => normalizer.Normalize(keyword))
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(MapKeyword)
            .Concat(BuiltInKeywords)
            .DistinctBy(x => x.Keyword)
            .ToArray();
    }

    public IReadOnlyList<PolicySignal> Detect(PolicyGuardRequest request, string normalizedMessage)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return [];
        }

        var matches = _keywords
            .Where(keyword => normalizedMessage.Contains(keyword.Keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(keyword => keyword.Keyword.Length)
            .ToArray();

        return matches
            .Where(match => !matches.Any(other =>
                other.Reason == match.Reason &&
                other.Keyword.Length > match.Keyword.Length &&
                other.Keyword.Contains(match.Keyword, StringComparison.OrdinalIgnoreCase)))
            .Select(keyword => new PolicySignal(
                "keyword",
                keyword.Reason.ToString(),
                keyword.Keyword,
                0.55m,
                1m,
                keyword.Reason,
                $"Exact keyword match: {keyword.Keyword}"))
            .ToArray();
    }

    private static (string Keyword, SupportCaseReason Reason) MapKeyword(string keyword)
    {
        var builtIn = BuiltInKeywords.FirstOrDefault(x =>
            string.Equals(x.Keyword, keyword, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(builtIn.Keyword))
        {
            return builtIn;
        }

        return (keyword, keyword.Contains("refund", StringComparison.OrdinalIgnoreCase)
            ? SupportCaseReason.RefundRequest
            : SupportCaseReason.PolicyException);
    }
}
