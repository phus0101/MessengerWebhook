using System.Text.RegularExpressions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public sealed class RegexPolicySignalDetector : IPolicySignalDetector
{
    private static readonly (Regex Pattern, string MatchedText, SupportCaseReason Reason)[] Patterns =
    {
        (new Regex(@"\bhoan\s+tien\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "hoan tien", SupportCaseReason.RefundRequest),
        (new Regex(@"\bhuy\s+don\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "huy don", SupportCaseReason.CancellationRequest),
        (new Regex(@"\bmien\s+phi\s+van\s+chuyen\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "mien phi van chuyen", SupportCaseReason.PolicyException),
        (new Regex(@"\bpr[o0]mpt\s+inject[i!1]on\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "prompt injection", SupportCaseReason.PromptInjection)
    };

    private readonly PolicyGuardOptions _options;

    public RegexPolicySignalDetector(IOptions<PolicyGuardOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<PolicySignal> Detect(PolicyGuardRequest request, string normalizedMessage)
    {
        if (!_options.EnableRegexDetector || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return [];
        }

        return Patterns
            .Where(pattern => pattern.Pattern.IsMatch(normalizedMessage))
            .Select(pattern => new PolicySignal(
                "regex",
                pattern.Reason.ToString(),
                pattern.MatchedText,
                0.45m,
                1m,
                pattern.Reason,
                $"Regex pattern match: {pattern.MatchedText}"))
            .ToArray();
    }
}
