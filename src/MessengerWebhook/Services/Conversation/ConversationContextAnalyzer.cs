using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation.Configuration;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace MessengerWebhook.Services.Conversation;

/// <summary>
/// Main service for analyzing conversation context and generating insights
/// </summary>
public class ConversationContextAnalyzer : IConversationContextAnalyzer
{
    private readonly PatternDetector _patternDetector;
    private readonly TopicAnalyzer _topicAnalyzer;
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ConversationContextAnalyzer> _logger;
    private readonly ConversationAnalysisOptions _options;

    public ConversationContextAnalyzer(
        PatternDetector patternDetector,
        TopicAnalyzer topicAnalyzer,
        IMemoryCache cache,
        ITenantContext tenantContext,
        ILogger<ConversationContextAnalyzer> logger,
        IOptions<ConversationAnalysisOptions> options)
    {
        _patternDetector = patternDetector;
        _topicAnalyzer = topicAnalyzer;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ConversationContext> AnalyzeAsync(
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (history.Count == 0)
        {
            return CreateEmptyContext();
        }

        // Check cache
        var cacheKey = GenerateCacheKey(history);
        if (_options.EnableCaching && _cache.TryGetValue<ConversationContext>(cacheKey, out var cachedContext))
        {
            _logger.LogDebug("Returning cached conversation context for {TurnCount} turns", history.Count);
            return cachedContext!;
        }

        var startTime = DateTime.UtcNow;

        // Limit to analysis window
        var recentHistory = history
            .TakeLast(_options.AnalysisWindowSize)
            .ToList();

        // Detect patterns
        var patterns = _options.EnablePatternDetection
            ? _patternDetector.DetectPatterns(recentHistory)
            : new List<ConversationPattern>();

        // Extract topics
        var topics = _options.EnableTopicAnalysis
            ? _topicAnalyzer.ExtractTopics(recentHistory)
            : new List<ConversationTopic>();

        // Calculate quality
        var quality = CalculateQuality(recentHistory, patterns);

        // Determine journey stage
        var stage = DetermineJourneyStage(patterns, topics, quality);

        // Generate insights
        var insights = _options.EnableInsightGeneration
            ? GenerateInsights(stage, patterns, topics, quality)
            : new List<ConversationInsight>();

        var context = new ConversationContext
        {
            CurrentStage = stage,
            Patterns = patterns,
            Topics = topics,
            Quality = quality,
            Insights = insights,
            TurnCount = recentHistory.Count,
            AnalyzedAt = DateTime.UtcNow
        };

        // Cache result
        if (_options.EnableCaching)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheDurationMinutes))
                .SetSize(1);
            _cache.Set(cacheKey, context, cacheOptions);
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation(
            "Conversation analysis completed in {Duration}ms - Stage: {Stage}, Patterns: {PatternCount}, Quality: {Quality:F1}",
            duration, stage, patterns.Count, quality.Score);

        return await Task.FromResult(context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ConversationContext> AnalyzeWithEmotionAsync(
        List<ConversationMessage> history,
        List<EmotionScore> emotionHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(emotionHistory);

        var context = await AnalyzeAsync(history, cancellationToken).ConfigureAwait(false);

        // Enhance insights with emotion data
        if (emotionHistory.Count > 0)
        {
            var recentEmotions = emotionHistory.TakeLast(3).ToList();

            // Calculate negative emotion prevalence
            var negativeEmotionCount = recentEmotions.Count(e =>
                e.PrimaryEmotion == EmotionType.Negative ||
                e.PrimaryEmotion == EmotionType.Frustrated);

            var negativeRatio = recentEmotions.Count > 0
                ? (double)negativeEmotionCount / recentEmotions.Count
                : 0;

            // Add emotion-based insights
            if (negativeRatio > 0.5 && !context.Insights.Any(i => i.Type == InsightType.AddressObjection))
            {
                context.Insights.Add(new ConversationInsight
                {
                    Type = InsightType.AddressObjection,
                    Message = "Customer showing negative emotions - address concerns",
                    Confidence = 0.8,
                    SuggestedAction = "Acknowledge concerns and provide reassurance"
                });
            }

            context.Metadata["negativeEmotionRatio"] = negativeRatio;
            context.Metadata["primaryEmotion"] = recentEmotions.LastOrDefault()?.PrimaryEmotion.ToString() ?? "Unknown";
        }

        return context;
    }

    /// <summary>
    /// Calculate conversation quality metrics
    /// </summary>
    private ConversationQuality CalculateQuality(
        List<ConversationMessage> history,
        List<ConversationPattern> patterns)
    {
        var userMessages = history.Where(m => m.Role == "user").ToList();

        if (userMessages.Count == 0)
        {
            return new ConversationQuality { Score = 0, Coherence = 0, Engagement = 0, Momentum = 0 };
        }

        // Calculate engagement (based on message length and frequency)
        var avgLength = userMessages.Average(m => m.Content.Length);
        var engagement = Math.Min(1.0, avgLength / 100.0); // Normalize to 0-1

        // Calculate coherence (inverse of topic shifts)
        var topicShifts = patterns.Count(p => p.Type == PatternType.TopicShift);
        var coherence = Math.Max(0, 1.0 - (topicShifts * 0.2));

        // Calculate momentum (comparing first half vs second half engagement)
        var momentum = CalculateMomentum(userMessages);

        // Overall score (weighted average)
        var score = (engagement * 0.4 + coherence * 0.3 + momentum * 0.3) * 100;

        return new ConversationQuality
        {
            Score = Math.Round(score, 1),
            Coherence = Math.Round(coherence, 2),
            Engagement = Math.Round(engagement, 2),
            Momentum = Math.Round(momentum, 2),
            Metrics = new Dictionary<string, double>
            {
                ["avgMessageLength"] = avgLength,
                ["messageCount"] = userMessages.Count,
                ["topicShifts"] = topicShifts
            }
        };
    }

    /// <summary>
    /// Calculate conversation momentum
    /// </summary>
    private double CalculateMomentum(List<ConversationMessage> userMessages)
    {
        if (userMessages.Count < 2)
            return 0.5; // Neutral

        var midPoint = userMessages.Count / 2;
        var firstHalfAvg = userMessages.Take(midPoint).Average(m => m.Content.Length);
        var secondHalfAvg = userMessages.Skip(midPoint).Average(m => m.Content.Length);

        if (firstHalfAvg == 0)
            return 0.5;

        var ratio = secondHalfAvg / firstHalfAvg;

        // Convert ratio to 0-1 scale (0.5 = neutral, >0.5 = increasing, <0.5 = decreasing)
        return Math.Min(1.0, Math.Max(0, ratio));
    }

    /// <summary>
    /// Determine customer journey stage based on patterns and context
    /// </summary>
    private JourneyStage DetermineJourneyStage(
        List<ConversationPattern> patterns,
        List<ConversationTopic> topics,
        ConversationQuality quality)
    {
        // Check for buying signals
        var buyingSignal = patterns.FirstOrDefault(p => p.Type == PatternType.BuyingSignal);
        if (buyingSignal != null && buyingSignal.Confidence >= _options.BuyingSignalThreshold)
        {
            return JourneyStage.Ready;
        }

        // Check for hesitation
        var hasHesitation = patterns.Any(p => p.Type == PatternType.Hesitation);
        var hasPriceSensitivity = patterns.Any(p => p.Type == PatternType.PriceSensitivity);

        if (hasHesitation || hasPriceSensitivity)
        {
            return JourneyStage.Considering;
        }

        // Check for engagement drop
        var hasEngagementDrop = patterns.Any(p => p.Type == PatternType.EngagementDrop);
        if (hasEngagementDrop && quality.Momentum < 0.3)
        {
            return JourneyStage.Stalled;
        }

        // Check for abandonment (very low quality)
        if (quality.Score < 20 && quality.Engagement < 0.2)
        {
            return JourneyStage.Abandoned;
        }

        // Default to browsing
        return JourneyStage.Browsing;
    }

    /// <summary>
    /// Generate actionable insights based on analysis
    /// </summary>
    private List<ConversationInsight> GenerateInsights(
        JourneyStage stage,
        List<ConversationPattern> patterns,
        List<ConversationTopic> topics,
        ConversationQuality quality)
    {
        var insights = new List<ConversationInsight>();

        // Stage-based insights
        switch (stage)
        {
            case JourneyStage.Ready:
                insights.Add(new ConversationInsight
                {
                    Type = InsightType.CloseSale,
                    Message = "Customer showing strong buying signals - ready to close",
                    Confidence = 0.9,
                    SuggestedAction = "Offer to finalize order with contact info"
                });
                break;

            case JourneyStage.Stalled:
                insights.Add(new ConversationInsight
                {
                    Type = InsightType.GiveSpace,
                    Message = "Conversation stalled - customer may need time",
                    Confidence = 0.8,
                    SuggestedAction = "Offer to follow up later, provide contact info"
                });
                break;

            case JourneyStage.Abandoned:
                insights.Add(new ConversationInsight
                {
                    Type = InsightType.EscalateToHuman,
                    Message = "Low engagement - consider human intervention",
                    Confidence = 0.7,
                    SuggestedAction = "Offer to connect with human agent"
                });
                break;
        }

        // Pattern-based insights
        var priceSensitivity = patterns.FirstOrDefault(p => p.Type == PatternType.PriceSensitivity);
        if (priceSensitivity != null && priceSensitivity.Confidence > 0.7)
        {
            insights.Add(new ConversationInsight
            {
                Type = InsightType.SuggestDiscount,
                Message = "Customer price-sensitive - consider offering discount",
                Confidence = priceSensitivity.Confidence,
                SuggestedAction = "Mention current promotions or bundle deals"
            });
        }

        var repeatQuestion = patterns.FirstOrDefault(p => p.Type == PatternType.RepeatQuestion);
        if (repeatQuestion != null)
        {
            insights.Add(new ConversationInsight
            {
                Type = InsightType.ProvideMoreInfo,
                Message = "Customer repeating questions - may need clearer information",
                Confidence = 0.85,
                SuggestedAction = "Provide more detailed explanation or examples"
            });
        }

        var informationGap = patterns.FirstOrDefault(p => p.Type == PatternType.InformationGap);
        if (informationGap != null)
        {
            insights.Add(new ConversationInsight
            {
                Type = InsightType.ProvideMoreInfo,
                Message = "Information gap detected - customer needs more details",
                Confidence = 0.8,
                SuggestedAction = "Proactively provide relevant product information"
            });
        }

        // Quality-based insights
        if (quality.Engagement < 0.3 && stage != JourneyStage.Stalled)
        {
            insights.Add(new ConversationInsight
            {
                Type = InsightType.GiveSpace,
                Message = "Low engagement - customer may need time to think",
                Confidence = 0.75,
                SuggestedAction = "Don't push, offer to follow up later"
            });
        }

        return insights;
    }

    /// <summary>
    /// Generate cache key based on conversation history
    /// </summary>
    private string GenerateCacheKey(List<ConversationMessage> history)
    {
        if (history.Count == 0)
            return $"conversation_context:{_tenantContext.TenantId}:empty";

        // Use stable hash (SHA256) for cache key to prevent collisions
        var content = string.Join("|", history.Select(m => $"{m.Role}:{m.Content}"));
        var hash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(content))
        )[..22]; // 22 chars = 132 bits

        return $"conversation_context:{_tenantContext.TenantId}:{history.Count}_{hash}";
    }

    /// <summary>
    /// Create empty context for empty history
    /// </summary>
    private ConversationContext CreateEmptyContext()
    {
        return new ConversationContext
        {
            CurrentStage = JourneyStage.Browsing,
            Patterns = new List<ConversationPattern>(),
            Topics = new List<ConversationTopic>(),
            Quality = new ConversationQuality { Score = 0, Coherence = 0, Engagement = 0, Momentum = 0 },
            Insights = new List<ConversationInsight>(),
            TurnCount = 0,
            AnalyzedAt = DateTime.UtcNow
        };
    }
}
