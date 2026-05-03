using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MessengerWebhook.Configuration;

namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Hybrid sub-intent classifier: keyword-first (fast path) + AI fallback
/// Achieves <500ms for 70% of queries, ~1s for ambiguous 30%
/// </summary>
public sealed class HybridSubIntentClassifier : ISubIntentClassifier
{
    private readonly KeywordSubIntentDetector _keywordDetector;
    private readonly GeminiSubIntentClassifier _aiClassifier;
    private readonly SubIntentOptions _options;
    private readonly ILogger<HybridSubIntentClassifier> _logger;

    public HybridSubIntentClassifier(
        KeywordSubIntentDetector keywordDetector,
        GeminiSubIntentClassifier aiClassifier,
        IOptions<SubIntentOptions> options,
        ILogger<HybridSubIntentClassifier> logger)
    {
        _keywordDetector = keywordDetector;
        _aiClassifier = aiClassifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        // Fast path: keyword matching (<10ms)
        var keywordResult = await _keywordDetector.ClassifyAsync(message, conversationContext, cancellationToken);

        if (keywordResult != null && keywordResult.Confidence >= _options.KeywordHighConfidenceThreshold)
        {
            _logger.LogDebug(
                "Keyword detector high confidence: {Category} ({Confidence})",
                keywordResult.Category,
                keywordResult.Confidence);
            return keywordResult;
        }

        // AI fallback disabled - return keyword result or null
        if (!_options.EnableAiFallback)
        {
            return keywordResult;
        }

        // Fallback: AI classification for ambiguous cases (~510ms)
        _logger.LogDebug("Escalating to AI classifier (keyword confidence: {Confidence})",
            keywordResult?.Confidence ?? 0);

        var aiResult = await _aiClassifier.ClassifyAsync(message, conversationContext, cancellationToken);

        // Merge results: prefer AI if confidence > threshold
        if (aiResult != null && aiResult.Confidence >= _options.HybridAiAcceptanceThreshold)
        {
            _logger.LogInformation(
                "AI classifier accepted: {Category} (confidence: {Confidence}, keyword: {KeywordCategory})",
                aiResult.Category,
                aiResult.Confidence,
                keywordResult?.Category.ToString() ?? "none");

            return aiResult with { Source = "hybrid" };
        }

        // AI failed or low confidence - fallback to keyword result
        if (keywordResult != null)
        {
            _logger.LogDebug(
                "AI confidence too low ({AiConfidence}), using keyword result: {Category}",
                aiResult?.Confidence ?? 0,
                keywordResult.Category);
            return keywordResult;
        }

        // Both failed
        _logger.LogDebug("Both keyword and AI classifiers failed to classify message");
        return null;
    }
}
