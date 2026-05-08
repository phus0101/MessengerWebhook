using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Policy;

public class PolicyGuardService : IPolicyGuardService
{
    private readonly SalesBotOptions _salesBotOptions;
    private readonly PolicyGuardOptions _policyOptions;
    private readonly IPolicyMessageNormalizer _normalizer;
    private readonly IPolicyRiskScorer _scorer;
    private readonly IPolicyIntentClassifier? _classifier;
    private readonly IPolicySignalDetector[] _detectors;
    private readonly ILogger<PolicyGuardService>? _logger;

    public PolicyGuardService(IOptions<SalesBotOptions> options)
        : this(
            options,
            Options.Create(new PolicyGuardOptions()),
            new DefaultPolicyMessageNormalizer(),
            new DefaultPolicyRiskScorer(Options.Create(new PolicyGuardOptions())),
            [
                new KeywordPolicySignalDetector(
                    Options.Create(new PolicyGuardOptions()),
                    new DefaultPolicyMessageNormalizer()),
                new RegexPolicySignalDetector(Options.Create(new PolicyGuardOptions())),
                new FuzzyPolicySignalDetector(Options.Create(new PolicyGuardOptions()))
            ],
            null,
            null)
    {
    }

    public PolicyGuardService(
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<PolicyGuardOptions> policyOptions,
        IPolicyMessageNormalizer normalizer,
        IPolicyRiskScorer scorer,
        IEnumerable<IPolicySignalDetector> detectors,
        IPolicyIntentClassifier? classifier = null,
        ILogger<PolicyGuardService>? logger = null)
    {
        _salesBotOptions = salesBotOptions.Value;
        _policyOptions = policyOptions.Value;
        _normalizer = normalizer;
        _scorer = scorer;
        _classifier = classifier;
        _detectors = detectors.ToArray();
        _logger = logger;
    }

    public async ValueTask<PolicyDecision> EvaluateAsync(
        PolicyGuardRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = _normalizer.Normalize(request.Message);
        _logger?.LogInformation(
            "Policy guard evaluating message. MessageLength={MessageLength} HasOpenSupportCase={HasOpenSupportCase} HasDraftOrder={HasDraftOrder} SemanticEnabled={SemanticEnabled}",
            request.Message.Length,
            request.HasOpenSupportCase,
            request.HasDraftOrder,
            _policyOptions.EnableSemanticClassifier);

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            _logger?.LogInformation("Policy guard skipped because normalized message is empty.");
            return new PolicyDecision(false, SupportCaseReason.ManualReview, string.Empty, PolicyAction.Allow, 0m, 0m, []);
        }

        // Dedup across detectors by (MatchedText, Reason) and keep the highest-weighted signal.
        // Previously the key included Detector → keyword+regex+fuzzy on the same span all survived
        // and got summed by the scorer, inflating risk.
        var signals = _detectors
            .SelectMany(detector => detector.Detect(request, normalizedMessage))
            .GroupBy(signal => new { signal.MatchedText, signal.Reason })
            .Select(group => group
                .OrderByDescending(signal => signal.Weight * signal.Confidence)
                .ThenByDescending(signal => signal.Confidence)
                .First())
            .ToArray();

        PolicyClassificationResult? classification = null;
        var semanticClassifierAttempted = ShouldAttemptSemanticClassifier(normalizedMessage, signals);
        if (semanticClassifierAttempted)
        {
            _logger?.LogInformation("Policy guard invoking semantic classifier. SignalCount={SignalCount}", signals.Length);
            try
            {
                classification = await _classifier!.ClassifyAsync(request, normalizedMessage, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Policy guard semantic classifier failed. Falling back to deterministic signals.");
            }
        }
        else
        {
            _logger?.LogInformation(
                "Policy guard skipped semantic classifier. Enabled={Enabled} ClassifierRegistered={ClassifierRegistered}",
                _policyOptions.EnableSemanticClassifier,
                _classifier != null);
        }

        var score = _scorer.Score(request, signals, classification);
        _logger?.LogInformation(
            "Policy guard decision completed. Action={Action} Reason={Reason} Score={Score} Confidence={Confidence} SignalCount={SignalCount} SemanticMatched={SemanticMatched}",
            score.Action,
            score.Reason,
            score.Score,
            score.Confidence,
            signals.Length,
            classification != null);
        return new PolicyDecision(
            score.Action is PolicyAction.SoftEscalate or PolicyAction.HardEscalate,
            score.Reason,
            score.Summary,
            score.Action,
            score.Score,
            score.Confidence,
            signals,
            semanticClassifierAttempted);
    }

    public PolicyDecision Evaluate(string message)
    {
        var normalizedMessage = _normalizer.Normalize(message);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return new PolicyDecision(false, SupportCaseReason.ManualReview, string.Empty, PolicyAction.Allow, 0m, 0m, []);
        }

        var request = new PolicyGuardRequest(message);
        var signals = _detectors
            .Where(detector => detector is KeywordPolicySignalDetector)
            .SelectMany(detector => detector.Detect(request, normalizedMessage))
            .ToArray();

        var score = _scorer.Score(request, signals);
        return new PolicyDecision(
            score.Action is PolicyAction.SoftEscalate or PolicyAction.HardEscalate,
            score.Reason,
            score.Summary,
            score.Action,
            score.Score,
            score.Confidence,
            signals);
    }

    private bool ShouldAttemptSemanticClassifier(string normalizedMessage, IReadOnlyList<PolicySignal> signals)
    {
        if (!_policyOptions.EnableSemanticClassifier || _classifier == null || HasHardEscalationSignal(signals))
        {
            return false;
        }

        return ContainsAny(normalizedMessage, "nguoi that", "nhan vien", "tu van vien", "shop ho tro", "ho tro vien", "gap nguoi", "noi chuyen", "manual", "support", "admin");
    }

    private static bool HasHardEscalationSignal(IReadOnlyList<PolicySignal> signals)
    {
        return signals.Any(signal => signal.Detector == "keyword" || signal.Reason == SupportCaseReason.PromptInjection);
    }

    private static bool ContainsAny(string value, params string[] phrases)
    {
        return phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    public string EnsureClosingCallToAction(string response)
    {
        var safeResponse = string.IsNullOrWhiteSpace(response)
            ? "Da em ho tro chi ngay day a."
            : response.Trim();

        if (safeResponse.Contains(_salesBotOptions.ClosingCallToAction, StringComparison.OrdinalIgnoreCase))
        {
            return safeResponse;
        }

        return $"{safeResponse}\n\n{_salesBotOptions.ClosingCallToAction}";
    }
}
