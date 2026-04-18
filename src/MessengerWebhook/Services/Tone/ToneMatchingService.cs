using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.Tone.Configuration;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.Tone;

/// <summary>
/// Service for matching bot tone to customer emotion and context
/// </summary>
public class ToneMatchingService : IToneMatchingService
{
    private readonly IMemoryCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ToneMatchingService> _logger;
    private readonly ToneMatchingOptions _options;

    public ToneMatchingService(
        IMemoryCache cache,
        ITenantContext tenantContext,
        ILogger<ToneMatchingService> logger,
        IOptions<ToneMatchingOptions> options)
    {
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ToneProfile> GenerateToneProfileAsync(
        EmotionScore emotion,
        VipProfile vipProfile,
        CustomerIdentity customer,
        int conversationTurnCount = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emotion);
        ArgumentNullException.ThrowIfNull(vipProfile);
        ArgumentNullException.ThrowIfNull(customer);

        if (conversationTurnCount < 0)
            throw new ArgumentOutOfRangeException(nameof(conversationTurnCount), "Must be >= 0");

        var context = new ToneContext
        {
            Emotion = emotion,
            VipProfile = vipProfile,
            Customer = customer,
            ConversationTurnCount = conversationTurnCount,
            IsFirstInteraction = customer.TotalOrders == 0 && conversationTurnCount <= 1
        };

        return await GenerateToneProfileAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ToneProfile> GenerateToneProfileAsync(
        ToneContext context,
        CancellationToken cancellationToken = default)
    {
        // Check cache
        if (_options.EnableCaching)
        {
            var cacheKey = GetCacheKey(context);
            if (_cache.TryGetValue<ToneProfile>(cacheKey, out var cached))
            {
                _logger.LogDebug("Tone profile cache hit for key: {CacheKey}", cacheKey);
                return cached!;
            }
        }

        // Analyze customer context
        var customerSignals = AnalyzeCustomerContext(context);

        // Map emotion to tone level
        var toneLevel = MapEmotionToToneLevel(context.Emotion, customerSignals);

        // Select pronoun
        var pronoun = SelectPronoun(customerSignals, toneLevel);
        var pronounText = GetPronounText(pronoun);

        // Detect escalation
        var (requiresEscalation, escalationReason) = DetectEscalation(context.Emotion, _options);

        // Build tone instructions
        var instructions = BuildToneInstructions(
            toneLevel,
            context.Emotion.PrimaryEmotion,
            requiresEscalation);

        // Create profile
        var profile = new ToneProfile
        {
            Level = toneLevel,
            Pronoun = pronoun,
            PronounText = pronounText,
            RequiresEscalation = requiresEscalation,
            EscalationReason = escalationReason,
            ToneInstructions = instructions,
            Metadata = new Dictionary<string, object>
            {
                ["emotion"] = context.Emotion.PrimaryEmotion.ToString(),
                ["emotion_confidence"] = context.Emotion.Confidence,
                ["vip_tier"] = context.VipProfile.Tier.ToString(),
                ["is_returning"] = customerSignals.IsReturning,
                ["conversation_turns"] = context.ConversationTurnCount
            }
        };

        // Cache result
        if (_options.EnableCaching)
        {
            var cacheKey = GetCacheKey(context);
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheDurationMinutes))
                .SetSize(1);
            _cache.Set(cacheKey, profile, cacheOptions);
        }

        _logger.LogInformation(
            "Generated tone profile: {Level} / {Pronoun} (emotion: {Emotion}, escalation: {Escalation})",
            toneLevel,
            pronounText,
            context.Emotion.PrimaryEmotion,
            requiresEscalation);

        return await Task.FromResult(profile).ConfigureAwait(false);
    }

    private CustomerContextSignals AnalyzeCustomerContext(ToneContext context)
    {
        var isVip = context.VipProfile.Tier == VipTier.Vip;
        var isReturning = context.VipProfile.Tier == VipTier.Returning;
        var isNew = context.VipProfile.Tier == VipTier.Standard;

        var hasOrders = context.Customer.TotalOrders > 0;
        var isHighValue = context.Customer.LifetimeValue > 1000000; // 1M VND

        var hasFailedDeliveries = context.Customer.FailedDeliveries > 0;
        var riskScore = hasOrders
            ? context.Customer.FailedDeliveries / (decimal)context.Customer.TotalOrders
            : 0;

        return new CustomerContextSignals
        {
            IsVip = isVip,
            IsReturning = isReturning,
            IsNew = isNew,
            HasOrders = hasOrders,
            IsHighValue = isHighValue,
            RiskScore = riskScore,
            IsFirstInteraction = context.IsFirstInteraction
        };
    }

    private ToneLevel MapEmotionToToneLevel(
        EmotionScore emotion,
        CustomerContextSignals context)
    {
        // VIP always gets Formal unless Excited
        if (context.IsVip && emotion.PrimaryEmotion != EmotionType.Excited)
            return ToneLevel.Formal;

        // Emotion-based mapping
        return emotion.PrimaryEmotion switch
        {
            EmotionType.Positive => context.IsReturning ? ToneLevel.Friendly : ToneLevel.Formal,
            EmotionType.Excited => ToneLevel.Casual,
            EmotionType.Neutral => context.IsNew ? ToneLevel.Formal : ToneLevel.Friendly,
            EmotionType.Negative => ToneLevel.Formal,  // Professional, empathetic
            EmotionType.Frustrated => ToneLevel.Formal, // Apologetic, careful
            _ => ToneLevel.Friendly
        };
    }

    private VietnamesePronoun SelectPronoun(
        CustomerContextSignals context,
        ToneLevel toneLevel)
    {
        // Default to neutral "bạn" when uncertain
        // In production, this would use customer age/gender from profile
        // For now, use tier + tone as proxy

        if (context.IsVip)
            return VietnamesePronoun.Chi; // Respectful default for VIP

        if (toneLevel == ToneLevel.Casual && context.IsReturning)
            return VietnamesePronoun.Ban; // Casual but safe

        if (toneLevel == ToneLevel.Friendly)
            return VietnamesePronoun.Ban; // Neutral friendly

        return VietnamesePronoun.Ban; // Safe default
    }

    private (bool requiresEscalation, string? reason) DetectEscalation(
        EmotionScore emotion,
        ToneMatchingOptions options)
    {
        if (!options.EnableEscalationDetection)
            return (false, null);

        // Check for frustration above threshold
        if (emotion.PrimaryEmotion == EmotionType.Frustrated &&
            emotion.Confidence >= options.FrustrationEscalationThreshold)
        {
            return (true, "Customer is frustrated - consider human handoff");
        }

        // Check for escalation patterns from emotion metadata
        if (emotion.Metadata.TryGetValue("escalation", out var escalation))
        {
            var pattern = escalation.ToString();
            return pattern switch
            {
                "anger_escalation" => (true, "Anger escalation detected - immediate attention needed"),
                "neutral_to_frustrated" => (true, "Customer frustration increasing - consider escalation"),
                "satisfaction_drop" => (true, "Customer satisfaction dropping - proactive intervention needed"),
                _ => (false, null)
            };
        }

        return (false, null);
    }

    private Dictionary<string, string> BuildToneInstructions(
        ToneLevel level,
        EmotionType emotion,
        bool requiresEscalation)
    {
        var instructions = new Dictionary<string, string>();

        // Base tone instruction
        instructions["tone_level"] = level switch
        {
            ToneLevel.Formal => "Sử dụng ngôn ngữ trang trọng, lịch sự, chuyên nghiệp",
            ToneLevel.Friendly => "Sử dụng ngôn ngữ thân thiện, gần gũi nhưng vẫn lịch sự",
            ToneLevel.Casual => "Sử dụng ngôn ngữ thoải mái, vui vẻ, gần gũi",
            _ => "Sử dụng ngôn ngữ thân thiện, lịch sự"
        };

        // Emotion-specific instruction
        instructions["emotion_adaptation"] = emotion switch
        {
            EmotionType.Positive => "Khách hàng đang vui vẻ - hãy duy trì năng lượng tích cực",
            EmotionType.Excited => "Khách hàng đang phấn khích - hãy nhiệt tình và hào hứng",
            EmotionType.Neutral => "Khách hàng bình thường - hãy chuyên nghiệp và hiệu quả",
            EmotionType.Negative => "Khách hàng không hài lòng - hãy thấu hiểu và tập trung giải pháp",
            EmotionType.Frustrated => "Khách hàng bực bội - hãy xin lỗi chân thành và đề xuất giải pháp cụ thể",
            _ => "Hãy thân thiện và chuyên nghiệp"
        };

        // Escalation instruction
        if (requiresEscalation)
        {
            instructions["escalation"] = "QUAN TRỌNG: Khách hàng cần được chăm sóc đặc biệt. Hãy đề xuất chuyển cho nhân viên nếu không giải quyết được ngay.";
        }

        return instructions;
    }

    private string GetPronounText(VietnamesePronoun pronoun)
    {
        return pronoun switch
        {
            VietnamesePronoun.Anh => "anh",
            VietnamesePronoun.Chi => "chị",
            VietnamesePronoun.Em => "em",
            VietnamesePronoun.Ban => "bạn",
            _ => _options.DefaultPronoun
        };
    }

    private string GetCacheKey(ToneContext context)
    {
        return $"tone:{_tenantContext.TenantId}:{context.Emotion.PrimaryEmotion}:{context.VipProfile.Tier}:{context.ConversationTurnCount}:{context.IsFirstInteraction}:{context.Customer.Id}";
    }

    /// <summary>
    /// Internal model for customer context analysis
    /// </summary>
    private class CustomerContextSignals
    {
        public bool IsVip { get; set; }
        public bool IsReturning { get; set; }
        public bool IsNew { get; set; }
        public bool HasOrders { get; set; }
        public bool IsHighValue { get; set; }
        public decimal RiskScore { get; set; }
        public bool IsFirstInteraction { get; set; }
    }
}
