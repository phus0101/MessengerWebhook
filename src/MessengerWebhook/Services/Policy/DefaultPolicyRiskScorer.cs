using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public sealed class DefaultPolicyRiskScorer : IPolicyRiskScorer
{
    private readonly PolicyGuardOptions _options;

    public DefaultPolicyRiskScorer(IOptions<PolicyGuardOptions> options)
    {
        _options = options.Value;
    }

    public PolicyScoreResult Score(
        PolicyGuardRequest request,
        IReadOnlyList<PolicySignal> signals,
        PolicyClassificationResult? classification = null)
    {
        var bestSignal = signals
            .OrderByDescending(signal => signal.Weight * signal.Confidence)
            .ThenByDescending(signal => signal.Confidence)
            .FirstOrDefault();

        var signalScore = signals.Sum(signal => signal.Weight * signal.Confidence);
        var classificationScore = GetClassificationScore(classification);
        var score = Math.Max(signalScore, classificationScore);

        if (request.HasOpenSupportCase)
        {
            score += _options.OpenSupportCaseBoost;
        }

        if (request.HasDraftOrder)
        {
            score += _options.DraftOrderBoost;
        }

        score += CountRepeatMentions(request) * _options.RepeatMentionBoost;
        score = Math.Min(score, 1m);

        var reason = classification?.Reason ?? bestSignal?.Reason ?? SupportCaseReason.ManualReview;
        var confidence = Math.Max(classification?.Confidence ?? 0m, bestSignal?.Confidence ?? 0m);
        var summary = classification?.Explanation ?? bestSignal?.Summary ?? string.Empty;

        if (bestSignal?.Detector is "keyword" or "regex" or "fuzzy" &&
            reason is SupportCaseReason.RefundRequest or SupportCaseReason.CancellationRequest or SupportCaseReason.PromptInjection)
        {
            return new PolicyScoreResult(PolicyAction.HardEscalate, reason, summary, Math.Max(score, _options.HardEscalateThreshold), Math.Max(confidence, 1m));
        }

        if (classification?.Intent == PolicySemanticIntent.ManualReview)
        {
            return new PolicyScoreResult(PolicyAction.SoftEscalate, reason, summary, Math.Min(score, _options.HardEscalateThreshold - 0.05m), confidence);
        }

        if (reason == SupportCaseReason.UnsupportedQuestion)
        {
            if (request.HasOpenSupportCase && score >= _options.SoftEscalateThreshold)
            {
                return new PolicyScoreResult(PolicyAction.SoftEscalate, reason, summary, Math.Min(score, _options.HardEscalateThreshold - 0.05m), confidence);
            }

            if (score >= _options.SafeReplyThreshold && (bestSignal?.Weight ?? 0m) >= 1m)
            {
                return new PolicyScoreResult(PolicyAction.SafeReply, reason, summary, score, confidence);
            }

            return new PolicyScoreResult(PolicyAction.Allow, reason, summary, score, confidence);
        }

        if (score >= _options.HardEscalateThreshold)
        {
            return new PolicyScoreResult(PolicyAction.HardEscalate, reason, summary, score, confidence);
        }

        if (score >= _options.SoftEscalateThreshold)
        {
            return new PolicyScoreResult(PolicyAction.SoftEscalate, reason, summary, score, confidence);
        }

        if (score >= _options.SafeReplyThreshold && (bestSignal?.Weight ?? 0m) >= 1m)
        {
            return new PolicyScoreResult(PolicyAction.SafeReply, reason, summary, score, confidence);
        }

        return new PolicyScoreResult(PolicyAction.Allow, reason, summary, score, confidence);
    }

    private decimal GetClassificationScore(PolicyClassificationResult? classification)
    {
        if (classification == null)
        {
            return 0m;
        }

        return classification.Intent switch
        {
            PolicySemanticIntent.ManualReview => 0.75m * classification.Confidence,
            PolicySemanticIntent.UnsupportedQuestion => 0.50m * classification.Confidence,
            PolicySemanticIntent.PolicyException => 0.65m * classification.Confidence,
            PolicySemanticIntent.RefundRequest or PolicySemanticIntent.CancellationRequest or PolicySemanticIntent.PromptInjection => 1m,
            _ => 0m
        };
    }

    private static int CountRepeatMentions(PolicyGuardRequest request)
    {
        if (request.RecentTurns == null || request.RecentTurns.Count == 0)
        {
            return 0;
        }

        return request.RecentTurns
            .Where((turn, index) => index != request.RecentTurns.Count - 1 ||
                !string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(turn.Content, request.Message, StringComparison.Ordinal))
            .Count(turn =>
                !string.IsNullOrWhiteSpace(turn.Content) &&
                request.Message.Contains(turn.Content, StringComparison.OrdinalIgnoreCase));
    }
}
