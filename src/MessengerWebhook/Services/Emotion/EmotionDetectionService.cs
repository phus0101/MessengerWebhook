using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Emotion.Configuration;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace MessengerWebhook.Services.Emotion;

/// <summary>
/// Main emotion detection service with context-aware analysis and caching
/// </summary>
public class EmotionDetectionService : IEmotionDetectionService
{
    private readonly RuleBasedEmotionDetector _ruleBasedDetector;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EmotionDetectionService> _logger;
    private readonly EmotionDetectionOptions _options;

    public EmotionDetectionService(
        IMemoryCache cache,
        ITenantContext tenantContext,
        ILogger<EmotionDetectionService> logger,
        IOptions<EmotionDetectionOptions> options)
    {
        _ruleBasedDetector = new RuleBasedEmotionDetector();
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<EmotionScore> DetectEmotionAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return CreateNeutralScore();
        }

        // Check cache if enabled
        if (_options.EnableCaching)
        {
            var cacheKey = GetCacheKey(message);
            if (_cache.TryGetValue<EmotionScore>(cacheKey, out var cachedScore))
            {
                _logger.LogDebug("Emotion detection cache hit");
                return cachedScore!;
            }

            // Detect and cache
            var score = _ruleBasedDetector.DetectEmotion(message);
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheDurationMinutes))
                .SetSize(1);
            _cache.Set(cacheKey, score, cacheOptions);

            _logger.LogInformation(
                "Detected emotion: {Emotion} (confidence: {Confidence:F2}) for message length: {Length}",
                score.PrimaryEmotion,
                score.Confidence,
                message.Length);

            _logger.LogDebug(
                "Message preview: {Message}",
                message.Length > 50 ? message[..Math.Min(50, message.Length)] : message);

            return await Task.FromResult(score).ConfigureAwait(false);
        }

        // No caching
        var result = _ruleBasedDetector.DetectEmotion(message);
        _logger.LogInformation(
            "Detected emotion: {Emotion} (confidence: {Confidence:F2})",
            result.PrimaryEmotion,
            result.Confidence);

        return await Task.FromResult(result).ConfigureAwait(false);
    }

    public async Task<EmotionScore> DetectEmotionWithContextAsync(
        string message,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return CreateNeutralScore();
        }

        if (!_options.EnableContextAnalysis || history == null || history.Count == 0)
        {
            return await DetectEmotionAsync(message, cancellationToken).ConfigureAwait(false);
        }

        // Detect current message emotion
        var currentEmotion = _ruleBasedDetector.DetectEmotion(message);

        // Analyze recent history for emotion trend
        var recentMessages = history
            .Where(m => m.Role == "user")
            .TakeLast(_options.ContextWindowSize)
            .ToList();

        if (recentMessages.Count == 0)
        {
            return currentEmotion;
        }

        // Detect emotions in recent messages
        var historicalEmotions = recentMessages
            .Select(m => _ruleBasedDetector.DetectEmotion(m.Content))
            .ToList();

        // Calculate weighted average with recent messages having higher weight
        var contextAwareScore = CalculateContextAwareScore(currentEmotion, historicalEmotions);

        // Detect emotion escalation
        var escalation = DetectEmotionEscalation(historicalEmotions, currentEmotion);
        if (escalation != null)
        {
            contextAwareScore.Metadata["escalation"] = escalation;
            _logger.LogWarning(
                "Emotion escalation detected: {Escalation}",
                escalation);
        }

        _logger.LogInformation(
            "Context-aware emotion: {Emotion} (confidence: {Confidence:F2}, history size: {HistorySize})",
            contextAwareScore.PrimaryEmotion,
            contextAwareScore.Confidence,
            recentMessages.Count);

        return await Task.FromResult(contextAwareScore).ConfigureAwait(false);
    }

    private EmotionScore CalculateContextAwareScore(
        EmotionScore currentEmotion,
        List<EmotionScore> historicalEmotions)
    {
        // Weight: current message 60%, recent history 40%
        const double currentWeight = 0.6;
        const double historyWeight = 0.4;

        var contextScores = new Dictionary<EmotionType, double>();

        // Initialize with current emotion scores
        foreach (var emotionType in Enum.GetValues<EmotionType>())
        {
            var currentScore = currentEmotion.Scores.GetValueOrDefault(emotionType, 0);

            // Calculate weighted average with historical scores
            var historicalAvg = historicalEmotions
                .Select(e => e.Scores.GetValueOrDefault(emotionType, 0))
                .DefaultIfEmpty(0)
                .Average();

            contextScores[emotionType] = (currentScore * currentWeight) + (historicalAvg * historyWeight);
        }

        // Find primary emotion
        var primaryEmotion = contextScores.OrderByDescending(x => x.Value).First().Key;
        var confidence = contextScores[primaryEmotion];

        return new EmotionScore
        {
            PrimaryEmotion = primaryEmotion,
            Scores = contextScores,
            Confidence = Math.Min(confidence, 1.0),
            DetectionMethod = "rule-based-context-aware",
            Metadata = new Dictionary<string, object>
            {
                ["context_window_size"] = historicalEmotions.Count,
                ["current_emotion"] = currentEmotion.PrimaryEmotion.ToString(),
                ["current_confidence"] = currentEmotion.Confidence
            }
        };
    }

    private string? DetectEmotionEscalation(
        List<EmotionScore> historicalEmotions,
        EmotionScore currentEmotion)
    {
        if (historicalEmotions.Count < 2)
        {
            return null;
        }

        // Check for escalation patterns
        var recentEmotions = historicalEmotions
            .Select(e => e.PrimaryEmotion)
            .Append(currentEmotion.PrimaryEmotion)
            .ToList();

        // Pattern: Neutral → Negative → Frustrated (check last 4 messages for 3-step pattern)
        if (recentEmotions.Count >= 4)
        {
            var last4 = recentEmotions.TakeLast(4).ToList();
            // Check if pattern exists in last 4: Neutral → Negative → Frustrated → any
            if (last4[0] == EmotionType.Neutral &&
                last4[1] == EmotionType.Negative &&
                last4[2] == EmotionType.Frustrated)
            {
                return "neutral_to_frustrated";
            }
        }

        // Pattern: Positive → Neutral → Negative (satisfaction drop)
        if (recentEmotions.Count >= 3)
        {
            var last3 = recentEmotions.TakeLast(3).ToList();
            if (last3[0] == EmotionType.Positive &&
                last3[1] == EmotionType.Neutral &&
                last3[2] == EmotionType.Negative)
            {
                return "satisfaction_drop";
            }
        }

        // Pattern: Negative → Frustrated (anger escalation)
        if (recentEmotions.Count >= 2)
        {
            var last2 = recentEmotions.TakeLast(2).ToList();
            if (last2[0] == EmotionType.Negative &&
                last2[1] == EmotionType.Frustrated)
            {
                return "anger_escalation";
            }
        }

        return null;
    }

    private EmotionScore CreateNeutralScore()
    {
        return new EmotionScore
        {
            PrimaryEmotion = EmotionType.Neutral,
            Scores = new Dictionary<EmotionType, double>
            {
                [EmotionType.Positive] = 0,
                [EmotionType.Neutral] = 1.0,
                [EmotionType.Negative] = 0,
                [EmotionType.Frustrated] = 0,
                [EmotionType.Excited] = 0
            },
            Confidence = 1.0,
            DetectionMethod = "rule-based"
        };
    }

    private string GetCacheKey(string message)
    {
        // Use hash for long messages to prevent collisions
        if (message.Length > 200)
        {
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(message))
            )[..16]; // 16 chars = 96 bits
            return $"emotion:{_tenantContext.TenantId}:{hash}";
        }
        return $"emotion:{_tenantContext.TenantId}:{message}";
    }
}
