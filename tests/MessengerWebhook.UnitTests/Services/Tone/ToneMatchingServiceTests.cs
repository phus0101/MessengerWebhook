using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Tone.Configuration;
using MessengerWebhook.Services.Tone.Models;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.Tone;

public class ToneMatchingServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<ToneMatchingService>> _loggerMock;
    private readonly ToneMatchingOptions _options;
    private readonly ToneMatchingService _service;

    public ToneMatchingServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        _loggerMock = new Mock<ILogger<ToneMatchingService>>();
        _options = new ToneMatchingOptions
        {
            EnableEmotionBasedAdaptation = true,
            EnableEscalationDetection = true,
            FrustrationEscalationThreshold = 0.7,
            DefaultPronoun = "bạn",
            EnableCaching = true,
            CacheDurationMinutes = 5
        };

        var optionsMock = new Mock<IOptions<ToneMatchingOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _service = new ToneMatchingService(_cache, _mockTenantContext.Object, _loggerMock.Object, optionsMock.Object);
    }

    [Fact]
    public async Task GenerateToneProfile_VipCustomerPositiveEmotion_ReturnsFormalTone()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Positive, 0.8);
        var vipProfile = CreateVipProfile(VipTier.Vip);
        var customer = CreateCustomer(totalOrders: 10);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Formal, profile.Level);
        Assert.Equal(VietnamesePronoun.Chi, profile.Pronoun);
        Assert.Equal("chị", profile.PronounText);
        Assert.False(profile.RequiresEscalation);
        Assert.Contains("trang trọng", profile.ToneInstructions["tone_level"]);
        Assert.Contains("vui vẻ", profile.ToneInstructions["emotion_adaptation"]);
    }

    [Fact]
    public async Task GenerateToneProfile_ReturningCustomerExcitedEmotion_ReturnsCasualTone()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Excited, 0.85);
        var vipProfile = CreateVipProfile(VipTier.Returning);
        var customer = CreateCustomer(totalOrders: 5);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Casual, profile.Level);
        Assert.Equal(VietnamesePronoun.Ban, profile.Pronoun);
        Assert.Equal("bạn", profile.PronounText);
        Assert.False(profile.RequiresEscalation);
        Assert.Contains("thoải mái", profile.ToneInstructions["tone_level"]);
        Assert.Contains("phấn khích", profile.ToneInstructions["emotion_adaptation"]);
    }

    [Fact]
    public async Task GenerateToneProfile_NewCustomerNeutralEmotion_ReturnsFormalTone()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Neutral, 0.7);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 0);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Formal, profile.Level);
        Assert.Equal(VietnamesePronoun.Ban, profile.Pronoun);
        Assert.Equal("bạn", profile.PronounText);
        Assert.False(profile.RequiresEscalation);
        Assert.Contains("trang trọng", profile.ToneInstructions["tone_level"]);
    }

    [Fact]
    public async Task GenerateToneProfile_FrustratedEmotionHighConfidence_ReturnsEscalationFlag()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Frustrated, 0.8);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 2);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.True(profile.RequiresEscalation);
        Assert.Equal("Customer is frustrated - consider human handoff", profile.EscalationReason);
        Assert.Contains("escalation", profile.ToneInstructions.Keys);
        Assert.Contains("chăm sóc đặc biệt", profile.ToneInstructions["escalation"]);
    }

    [Fact]
    public async Task GenerateToneProfile_FrustratedEmotionLowConfidence_NoEscalation()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Frustrated, 0.5);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 2);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.False(profile.RequiresEscalation);
        Assert.Null(profile.EscalationReason);
    }

    [Fact]
    public async Task GenerateToneProfile_AngerEscalationPattern_ReturnsEscalationFlag()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Frustrated, 0.6);
        emotion.Metadata["escalation"] = "anger_escalation";
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 3);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.True(profile.RequiresEscalation);
        Assert.Equal("Anger escalation detected - immediate attention needed", profile.EscalationReason);
    }

    [Fact]
    public async Task GenerateToneProfile_NeutralToFrustratedPattern_ReturnsEscalationFlag()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Frustrated, 0.6);
        emotion.Metadata["escalation"] = "neutral_to_frustrated";
        var vipProfile = CreateVipProfile(VipTier.Returning);
        var customer = CreateCustomer(totalOrders: 5);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.True(profile.RequiresEscalation);
        Assert.Contains("frustration increasing", profile.EscalationReason);
    }

    [Fact]
    public async Task GenerateToneProfile_SatisfactionDropPattern_ReturnsEscalationFlag()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Negative, 0.7);
        emotion.Metadata["escalation"] = "satisfaction_drop";
        var vipProfile = CreateVipProfile(VipTier.Vip);
        var customer = CreateCustomer(totalOrders: 15);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.True(profile.RequiresEscalation);
        Assert.Contains("satisfaction dropping", profile.EscalationReason);
    }

    [Fact]
    public async Task GenerateToneProfile_NegativeEmotion_ReturnsFormalEmpathetic()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Negative, 0.75);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 1);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Formal, profile.Level);
        Assert.Contains("trang trọng", profile.ToneInstructions["tone_level"]);
        Assert.Contains("không hài lòng", profile.ToneInstructions["emotion_adaptation"]);
        Assert.Contains("thấu hiểu", profile.ToneInstructions["emotion_adaptation"]);
    }

    [Fact]
    public async Task GenerateToneProfile_CachingEnabled_ReturnsCachedResult()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Positive, 0.8);
        var vipProfile = CreateVipProfile(VipTier.Returning);
        var customer = CreateCustomer(totalOrders: 5);

        // Act - First call
        var profile1 = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Act - Second call with same inputs
        var profile2 = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert - Should return same instance from cache
        Assert.Same(profile1, profile2);
    }

    [Fact]
    public async Task GenerateToneProfile_PronounDefaultsToBan_WhenUncertain()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Neutral, 0.7);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 1);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(VietnamesePronoun.Ban, profile.Pronoun);
        Assert.Equal("bạn", profile.PronounText);
    }

    [Fact]
    public async Task GenerateToneProfile_ToneInstructionsInVietnamese_GrammaticallyCorrect()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Positive, 0.8);
        var vipProfile = CreateVipProfile(VipTier.Returning);
        var customer = CreateCustomer(totalOrders: 3);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.NotEmpty(profile.ToneInstructions);
        Assert.Contains("tone_level", profile.ToneInstructions.Keys);
        Assert.Contains("emotion_adaptation", profile.ToneInstructions.Keys);

        // Verify Vietnamese text is present
        var allInstructions = string.Join(" ", profile.ToneInstructions.Values);
        Assert.Matches(@"[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđ]", allInstructions);
    }

    [Fact]
    public async Task GenerateToneProfile_MetadataPopulated_Correctly()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Excited, 0.9);
        var vipProfile = CreateVipProfile(VipTier.Vip);
        var customer = CreateCustomer(totalOrders: 20);
        var conversationTurns = 5;

        // Act
        var profile = await _service.GenerateToneProfileAsync(
            emotion, vipProfile, customer, conversationTurns);

        // Assert
        Assert.NotEmpty(profile.Metadata);
        Assert.Equal("Excited", profile.Metadata["emotion"]);
        Assert.Equal(0.9, profile.Metadata["emotion_confidence"]);
        Assert.Equal("Vip", profile.Metadata["vip_tier"]);
        Assert.Equal(5, profile.Metadata["conversation_turns"]);
    }

    [Fact]
    public async Task GenerateToneProfile_VipExcitedEmotion_ReturnsCasualTone()
    {
        // Arrange - VIP with Excited emotion should get Casual tone (exception to VIP=Formal rule)
        var emotion = CreateEmotionScore(EmotionType.Excited, 0.85);
        var vipProfile = CreateVipProfile(VipTier.Vip);
        var customer = CreateCustomer(totalOrders: 25);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Casual, profile.Level);
        Assert.Equal(VietnamesePronoun.Chi, profile.Pronoun); // Still respectful pronoun for VIP
    }

    [Fact]
    public async Task GenerateToneProfile_ReturningCustomerPositiveEmotion_ReturnsFriendlyTone()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Positive, 0.8);
        var vipProfile = CreateVipProfile(VipTier.Returning);
        var customer = CreateCustomer(totalOrders: 7);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.Equal(ToneLevel.Friendly, profile.Level);
        Assert.Contains("thân thiện", profile.ToneInstructions["tone_level"]);
    }

    [Fact]
    public async Task GenerateToneProfile_EscalationDisabled_NoEscalationFlag()
    {
        // Arrange
        _options.EnableEscalationDetection = false;
        var emotion = CreateEmotionScore(EmotionType.Frustrated, 0.9);
        var vipProfile = CreateVipProfile(VipTier.Standard);
        var customer = CreateCustomer(totalOrders: 2);

        // Act
        var profile = await _service.GenerateToneProfileAsync(emotion, vipProfile, customer);

        // Assert
        Assert.False(profile.RequiresEscalation);
        Assert.Null(profile.EscalationReason);
    }

    [Fact]
    public async Task GenerateToneProfile_UsingToneContext_WorksCorrectly()
    {
        // Arrange
        var context = new ToneContext
        {
            Emotion = CreateEmotionScore(EmotionType.Positive, 0.8),
            VipProfile = CreateVipProfile(VipTier.Returning),
            Customer = CreateCustomer(totalOrders: 5),
            ConversationTurnCount = 3,
            IsFirstInteraction = false
        };

        // Act
        var profile = await _service.GenerateToneProfileAsync(context);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(ToneLevel.Friendly, profile.Level);
        Assert.Equal(3, profile.Metadata["conversation_turns"]);
    }

    // Helper methods
    private EmotionScore CreateEmotionScore(EmotionType primaryEmotion, double confidence)
    {
        return new EmotionScore
        {
            PrimaryEmotion = primaryEmotion,
            Confidence = confidence,
            Scores = new Dictionary<EmotionType, double>
            {
                { primaryEmotion, confidence }
            },
            DetectionMethod = "rule-based",
            Metadata = new Dictionary<string, object>()
        };
    }

    private VipProfile CreateVipProfile(VipTier tier)
    {
        return new VipProfile
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            IsVip = tier == VipTier.Vip,
            TotalOrders = tier == VipTier.Vip ? 10 : (tier == VipTier.Returning ? 3 : 0),
            LifetimeValue = tier == VipTier.Vip ? 5000000 : (tier == VipTier.Returning ? 1500000 : 0),
            GreetingStyle = tier == VipTier.Vip ? "VIP_WARM_GREETING" : "STANDARD_GREETING"
        };
    }

    private CustomerIdentity CreateCustomer(int totalOrders = 0, int failedDeliveries = 0)
    {
        return new CustomerIdentity
        {
            Id = Guid.NewGuid(),
            FacebookPSID = "test-psid-" + Guid.NewGuid(),
            TotalOrders = totalOrders,
            SuccessfulDeliveries = totalOrders - failedDeliveries,
            FailedDeliveries = failedDeliveries,
            LifetimeValue = totalOrders * 200000, // 200k per order
            LastInteractionAt = DateTime.UtcNow
        };
    }
}
