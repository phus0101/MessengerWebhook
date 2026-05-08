using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public sealed class FuzzyPolicySignalDetector : IPolicySignalDetector
{
    private readonly PolicyGuardOptions _options;
    private static readonly (string Phrase, SupportCaseReason Reason)[] Phrases =
    {
        ("prompt injection", SupportCaseReason.PromptInjection),
        ("hoan tien", SupportCaseReason.RefundRequest),
        ("huy don", SupportCaseReason.CancellationRequest),
        ("mien phi van chuyen", SupportCaseReason.PolicyException)
    };

    public FuzzyPolicySignalDetector(IOptions<PolicyGuardOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<PolicySignal> Detect(PolicyGuardRequest request, string normalizedMessage)
    {
        if (!_options.EnableFuzzyDetector || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return [];
        }

        foreach (var phrase in Phrases)
        {
            var similarity = CalculateSimilarity(normalizedMessage, phrase.Phrase);
            if (similarity < 0.84m)
            {
                continue;
            }

            return
            [
                new PolicySignal(
                    "fuzzy",
                    phrase.Reason.ToString(),
                    phrase.Phrase,
                    0.20m * similarity,
                    similarity,
                    phrase.Reason,
                    $"Fuzzy phrase match: {phrase.Phrase}")
            ];
        }

        return [];
    }

    private static decimal CalculateSimilarity(string input, string phrase)
    {
        if (input.Contains(phrase, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var phraseTokenCount = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var inputTokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (phraseTokenCount == 0 || inputTokens.Length == 0)
        {
            return 0m;
        }

        var bestSimilarity = 0m;
        for (var index = 0; index <= inputTokens.Length - phraseTokenCount; index++)
        {
            var candidate = string.Join(' ', inputTokens.Skip(index).Take(phraseTokenCount));
            var distance = LevenshteinDistance(candidate, phrase);
            var length = Math.Max(candidate.Length, phrase.Length);
            if (length == 0)
            {
                continue;
            }

            bestSimilarity = Math.Max(bestSimilarity, 1m - (decimal)distance / length);
        }

        return bestSimilarity;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        var matrix = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= target.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }
}
