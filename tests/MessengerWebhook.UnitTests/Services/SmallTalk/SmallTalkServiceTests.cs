using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SmallTalk.Configuration;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tone.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.SmallTalk;

public class SmallTalkServiceTests
{
    private readonly SmallTalkDetector _detector;
    private readonly Mock<ILogger<SmallTalkService>> _loggerMock;
    private readonly SmallTalkOptions _options;
    private readonly SmallTalkService _service;

    public SmallTalkServiceTests()
    {
        _detector = new SmallTalkDetector();
        _loggerMock = new Mock<ILogger<SmallTalkService>>();
        _options = new SmallTalkOptions
        {
            EnableSmallTalkDetection = true,
            EnableContextAwareGreetings = true,
            SmallTalkConfidenceThreshold = 0.7,
            MaxSmallTalkTurns = 3,
            EnableSoftTransitions = true
        };

        _service = new SmallTalkService(
            _detector,
            _loggerMock.Object,
            Options.Create(_options));
    }

    private SmallTalkContext CreateSmallTalkContext(
        string message,
        EmotionScore? emotion = null,
        VipTier vipTier = VipTier.Standard,
        bool isReturning = false,
        int turnCount = 1,
        JourneyStage stage = JourneyStage.Browsing,
        TimeOfDay timeOfDay = TimeOfDay.Morning)
    {
        return new SmallTalkContext
        {
            Message = message,
            Emotion = emotion ?? CreateEmotionScore(EmotionType.Positive),
            ToneProfile = CreateToneProfile(),
            ConversationContext = CreateConversationContext(stage),
            VipProfile = CreateVipProfile(vipTier),
            IsReturningCustomer = isReturning,
            ConversationTurnCount = turnCount,
            TimeOfDay = timeOfDay
        };
    }

    private EmotionScore CreateEmotionScore(EmotionType primaryEmotion)
    {
        return new EmotionScore
        {
            PrimaryEmotion = primaryEmotion,
            Confidence = 0.85,
            Scores = new Dictionary<EmotionType, double>
            {
                { primaryEmotion, 0.85 }
            }
        };
    }

    private ToneProfile CreateToneProfile()
    {
        return new ToneProfile
        {
            Level = ToneLevel.Casual,
            PronounText = "bạn",
            ToneInstructions = new Dictionary<string, string>(),
            RequiresEscalation = false
        };
    }

    private ConversationContext CreateConversationContext(JourneyStage stage, List<ConversationPattern>? patterns = null)
    {
        return new ConversationContext
        {
            CurrentStage = stage,
            Patterns = patterns ?? new List<ConversationPattern>(),
            Quality = new ConversationQuality { Score = 8.0 },
            Insights = new List<ConversationInsight>()
        };
    }

    private VipProfile CreateVipProfile(VipTier tier)
    {
        return new VipProfile
        {
            Tier = tier,
            TotalOrders = tier == VipTier.Vip ? 5 : 0,
            LifetimeValue = tier == VipTier.Vip ? 5000000 : 0
        };
    }

    [Fact]
    public async Task AnalyzeAsync_CasualGreeting_ReturnsSmallTalkResponse()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "hi sốp",
            isReturning: true,
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.Greeting, response.Intent);
        Assert.Equal(TransitionReadiness.StayInSmallTalk, response.TransitionReadiness);
        Assert.Contains("Chào", response.SuggestedResponse);
        Assert.Contains("😊", response.SuggestedResponse);
        Assert.Contains("?", response.SuggestedResponse);
        Assert.Contains("hỗ trợ", response.SuggestedResponse, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Confidence >= 0.7);
    }

    [Fact]
    public async Task AnalyzeAsync_CheckInMessage_ReturnsCheckInIntent()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "có ai không",
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.CheckIn, response.Intent);
        Assert.Contains("Dạ em đây ạ!", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_BusinessIntent_ReturnsNotSmallTalk()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "cho em xem sản phẩm",
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.False(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.None, response.Intent);
        Assert.Equal(TransitionReadiness.ReadyForBusiness, response.TransitionReadiness);
    }

    [Fact]
    public async Task AnalyzeAsync_AfterMaxTurns_ReturnsSoftOffer()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "chào shop",
            turnCount: 3);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(TransitionReadiness.SoftOffer, response.TransitionReadiness);
        Assert.Contains("Có gì em giúp được không ạ?", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_VipCustomer_ReturnsPersonalizedGreeting()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "hello",
            vipTier: VipTier.Vip,
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Contains("chị", response.SuggestedResponse);
        Assert.Contains("vui được phục vụ", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturningCustomer_ReturnsWelcomeBackGreeting()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "chào",
            isReturning: true,
            vipTier: VipTier.Standard,
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Contains("Vui được gặp lại bạn", response.SuggestedResponse);
        Assert.Contains("?", response.SuggestedResponse);
        Assert.Contains("hỗ trợ", response.SuggestedResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_MorningGreeting_ReturnsTimeAwareGreeting()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "hi",
            timeOfDay: TimeOfDay.Morning,
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Contains("buổi sáng", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_ExcitedEmotion_ReturnsCasualGreeting()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "alo",
            emotion: CreateEmotionScore(EmotionType.Excited),
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Contains("Alo bạn!", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_BuyingSignalDetected_ReturnsReadyForBusiness()
    {
        // Arrange
        var patterns = new List<ConversationPattern>
        {
            new ConversationPattern
            {
                Type = PatternType.BuyingSignal,
                Confidence = 0.9,
                Description = "Customer showing buying intent"
            }
        };
        var context = CreateSmallTalkContext(
            message: "hi",
            stage: JourneyStage.Ready,
            turnCount: 1);
        context.ConversationContext.Patterns = patterns;

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(TransitionReadiness.ReadyForBusiness, response.TransitionReadiness);
        Assert.Contains("tư vấn sản phẩm", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_LowConfidence_ReturnsNotSmallTalk()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "this is a very long message that doesn't match small talk patterns clearly",
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.False(response.IsSmallTalk);
        Assert.Equal(TransitionReadiness.ReadyForBusiness, response.TransitionReadiness);
    }

    [Fact]
    public async Task AnalyzeAsync_DisabledFeature_ReturnsNotSmallTalk()
    {
        // Arrange
        var disabledOptions = new SmallTalkOptions
        {
            EnableSmallTalkDetection = false
        };
        var service = new SmallTalkService(
            _detector,
            _loggerMock.Object,
            Options.Create(disabledOptions));

        var context = CreateSmallTalkContext(message: "hi");

        // Act
        var response = await service.AnalyzeAsync(context);

        // Assert
        Assert.False(response.IsSmallTalk);
        Assert.Equal(0.0, response.Confidence);
    }

    [Fact]
    public async Task AnalyzeAsync_Pleasantry_ReturnsPleasantryIntent()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "cảm ơn shop",
            turnCount: 1);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.Pleasantry, response.Intent);
        Assert.Contains("cảm ơn", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_Acknowledgment_ReturnsAcknowledgmentIntent()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "ok",
            turnCount: 2);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.Acknowledgment, response.Intent);
        Assert.Contains("vâng", response.SuggestedResponse);
    }

    [Fact]
    public async Task AnalyzeAsync_MetadataPopulated_ContainsContextInfo()
    {
        // Arrange
        var context = CreateSmallTalkContext(
            message: "hi",
            vipTier: VipTier.Vip,
            isReturning: true,
            turnCount: 2,
            timeOfDay: TimeOfDay.Afternoon);

        // Act
        var response = await _service.AnalyzeAsync(context);

        // Assert
        Assert.True(response.Metadata.ContainsKey("timeOfDay"));
        Assert.Equal("Afternoon", response.Metadata["timeOfDay"]);
        Assert.True(response.Metadata.ContainsKey("isReturning"));
        Assert.True((bool)response.Metadata["isReturning"]);
        Assert.True(response.Metadata.ContainsKey("turnCount"));
        Assert.Equal(2, response.Metadata["turnCount"]);
        Assert.True(response.Metadata.ContainsKey("vipTier"));
        Assert.Equal("Vip", response.Metadata["vipTier"]);
    }

    [Fact]
    public async Task AnalyzeAsync_ConvenienceOverload_WorksCorrectly()
    {
        // Arrange
        var emotion = CreateEmotionScore(EmotionType.Positive);
        var toneProfile = CreateToneProfile();
        var conversationContext = CreateConversationContext(JourneyStage.Browsing);
        var vipProfile = CreateVipProfile(VipTier.Standard);

        // Act
        var response = await _service.AnalyzeAsync(
            "chào shop",
            emotion,
            toneProfile,
            conversationContext,
            vipProfile,
            isReturningCustomer: false,
            conversationTurnCount: 1);

        // Assert
        Assert.True(response.IsSmallTalk);
        Assert.Equal(SmallTalkIntent.Greeting, response.Intent);
    }
}
