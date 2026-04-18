using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Conversation.Configuration;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Conversation;

public class ConversationContextAnalyzerTests
{
    private readonly ConversationContextAnalyzer _analyzer;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<ConversationContextAnalyzer>> _loggerMock;
    private readonly IMemoryCache _cache;
    private readonly ConversationAnalysisOptions _options;

    public ConversationContextAnalyzerTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        _loggerMock = new Mock<ILogger<ConversationContextAnalyzer>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _options = new ConversationAnalysisOptions
        {
            AnalysisWindowSize = 10,
            EnablePatternDetection = true,
            EnableTopicAnalysis = true,
            EnableInsightGeneration = true,
            BuyingSignalThreshold = 0.7,
            RepeatQuestionThreshold = 0.8,
            RepeatQuestionWindow = 5,
            EnableCaching = true,
            CacheDurationMinutes = 10
        };

        var patternDetector = new PatternDetector();
        var topicAnalyzer = new TopicAnalyzer();

        _analyzer = new ConversationContextAnalyzer(
            patternDetector,
            topicAnalyzer,
            _cache,
            _mockTenantContext.Object,
            _loggerMock.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyHistory_ReturnsEmptyContext()
    {
        // Arrange
        var history = new List<ConversationMessage>();

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Equal(JourneyStage.Browsing, context.CurrentStage);
        Assert.Empty(context.Patterns);
        Assert.Empty(context.Topics);
        Assert.Equal(0, context.Quality.Score);
        Assert.Empty(context.Insights);
        Assert.Equal(0, context.TurnCount);
    }

    [Fact]
    public async Task AnalyzeAsync_BuyingSignalDetected_ReturnsReadyStage()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm này" },
            new() { Role = "model", Content = "Dạ đây ạ chị" },
            new() { Role = "user", Content = "Đặt luôn nha" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Equal(JourneyStage.Ready, context.CurrentStage);
        Assert.Contains(context.Patterns, p => p.Type == PatternType.BuyingSignal);
        Assert.Contains(context.Insights, i => i.Type == InsightType.CloseSale);
    }

    [Fact]
    public async Task AnalyzeAsync_RepeatQuestionDetected_ReturnsRepeatQuestionPattern()
    {
        // Arrange - Use very similar wording to exceed 0.8 similarity threshold
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Sản phẩm này có chất lượng tốt không ạ" },
            new() { Role = "model", Content = "Dạ rất tốt ạ" },
            new() { Role = "user", Content = "Sản phẩm này có chất lượng tốt không vậy" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Contains(context.Patterns, p => p.Type == PatternType.RepeatQuestion);
        Assert.Contains(context.Insights, i => i.Type == InsightType.ProvideMoreInfo);
    }

    [Fact]
    public async Task AnalyzeAsync_TopicShiftDetected_ReturnsTopicShiftPattern()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem kem dưỡng da" },
            new() { Role = "model", Content = "Dạ đây ạ" },
            new() { Role = "user", Content = "Giao hàng mất bao lâu" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Contains(context.Patterns, p => p.Type == PatternType.TopicShift);
    }

    [Fact]
    public async Task AnalyzeAsync_HesitationDetected_ReturnsConsideringStage()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Sản phẩm này tốt không" },
            new() { Role = "model", Content = "Dạ rất tốt ạ" },
            new() { Role = "user", Content = "Để em suy nghĩ thêm" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Equal(JourneyStage.Considering, context.CurrentStage);
        Assert.Contains(context.Patterns, p => p.Type == PatternType.Hesitation);
    }

    [Fact]
    public async Task AnalyzeAsync_PriceSensitivityDetected_ReturnsPriceSensitivityPattern()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Giá bao nhiêu vậy" },
            new() { Role = "model", Content = "Dạ 500k ạ" },
            new() { Role = "user", Content = "Hơi đắt quá, có giảm giá không" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Contains(context.Patterns, p => p.Type == PatternType.PriceSensitivity);
        Assert.Contains(context.Insights, i => i.Type == InsightType.SuggestDiscount);
    }

    [Fact]
    public async Task AnalyzeAsync_EngagementDrop_ReturnsEngagementDropPattern()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm kem dưỡng da tốt nhất của shop" },
            new() { Role = "model", Content = "Dạ đây ạ" },
            new() { Role = "user", Content = "Sản phẩm này có tốt không và giá bao nhiêu" },
            new() { Role = "model", Content = "Dạ tốt lắm ạ" },
            new() { Role = "user", Content = "Ok" },
            new() { Role = "model", Content = "Dạ vâng" },
            new() { Role = "user", Content = "Uh" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Contains(context.Patterns, p => p.Type == PatternType.EngagementDrop);
    }

    [Fact]
    public async Task AnalyzeAsync_JourneyStageProgression_BrowsingToConsideringToReady()
    {
        // Arrange - Browsing
        var browsingHistory = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm" },
            new() { Role = "model", Content = "Dạ đây ạ" }
        };

        // Act - Browsing
        var browsingContext = await _analyzer.AnalyzeAsync(browsingHistory);

        // Assert - Browsing
        Assert.Equal(JourneyStage.Browsing, browsingContext.CurrentStage);

        // Arrange - Considering
        var consideringHistory = new List<ConversationMessage>(browsingHistory)
        {
            new() { Role = "user", Content = "Hơi đắt, để em xem thêm" }
        };

        // Act - Considering
        var consideringContext = await _analyzer.AnalyzeAsync(consideringHistory);

        // Assert - Considering
        Assert.Equal(JourneyStage.Considering, consideringContext.CurrentStage);

        // Arrange - Ready
        var readyHistory = new List<ConversationMessage>(consideringHistory)
        {
            new() { Role = "model", Content = "Dạ vâng" },
            new() { Role = "user", Content = "Thôi đặt luôn" }
        };

        // Act - Ready
        var readyContext = await _analyzer.AnalyzeAsync(readyHistory);

        // Assert - Ready
        Assert.Equal(JourneyStage.Ready, readyContext.CurrentStage);
    }

    [Fact]
    public async Task AnalyzeAsync_HighQualityConversation_ReturnsHighQualityScore()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm kem dưỡng da tốt nhất" },
            new() { Role = "model", Content = "Dạ đây ạ chị" },
            new() { Role = "user", Content = "Sản phẩm này có thành phần gì vậy" },
            new() { Role = "model", Content = "Dạ có vitamin C và hyaluronic acid ạ" },
            new() { Role = "user", Content = "Giá bao nhiêu và giao hàng mất bao lâu" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.True(context.Quality.Score > 50);
        Assert.True(context.Quality.Engagement > 0.3);
    }

    [Fact]
    public async Task AnalyzeAsync_CachingWorks_ReturnsCachedResult()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Test message" },
            new() { Role = "model", Content = "Response" }
        };

        // Act - First call
        var context1 = await _analyzer.AnalyzeAsync(history);
        var analyzedAt1 = context1.AnalyzedAt;

        // Wait a bit to ensure timestamp would differ if recalculated
        await Task.Delay(10);

        // Act - Second call (should be cached)
        var context2 = await _analyzer.AnalyzeAsync(history);
        var analyzedAt2 = context2.AnalyzedAt;

        // Assert - Same timestamp means cached
        Assert.Equal(analyzedAt1, analyzedAt2);
    }

    [Fact]
    public async Task AnalyzeWithEmotionAsync_NegativeEmotions_AddsAddressObjectionInsight()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Sản phẩm này không tốt" },
            new() { Role = "model", Content = "Dạ em xin lỗi" }
        };

        var emotionHistory = new List<EmotionScore>
        {
            new() { PrimaryEmotion = EmotionType.Negative, Confidence = 0.9 },
            new() { PrimaryEmotion = EmotionType.Frustrated, Confidence = 0.8 }
        };

        // Act
        var context = await _analyzer.AnalyzeWithEmotionAsync(history, emotionHistory);

        // Assert
        Assert.Contains(context.Insights, i => i.Type == InsightType.AddressObjection);
        Assert.True(context.Metadata.ContainsKey("negativeEmotionRatio"));
    }

    [Fact]
    public async Task AnalyzeAsync_TopicExtraction_IdentifiesProductAndPriceTopics()
    {
        // Arrange
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm kem dưỡng da" },
            new() { Role = "model", Content = "Dạ đây ạ" },
            new() { Role = "user", Content = "Giá bao nhiêu vậy" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.Contains(context.Topics, t => t.Name == "product");
        Assert.Contains(context.Topics, t => t.Name == "price");
        Assert.True(context.Topics.Count >= 2);
    }

    [Fact]
    public async Task AnalyzeAsync_WindowSizeLimit_OnlyAnalyzesRecentMessages()
    {
        // Arrange - Create 15 messages (more than window size of 10)
        var history = new List<ConversationMessage>();
        for (int i = 0; i < 15; i++)
        {
            history.Add(new ConversationMessage { Role = "user", Content = $"Message {i}" });
            history.Add(new ConversationMessage { Role = "model", Content = $"Response {i}" });
        }

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert - Should only analyze last 10 messages
        Assert.Equal(10, context.TurnCount);
    }

    [Fact]
    public async Task AnalyzeAsync_StalledConversation_ReturnsStalledStage()
    {
        // Arrange - Long messages at start, very short at end
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm kem dưỡng da tốt nhất của shop" },
            new() { Role = "model", Content = "Dạ đây ạ" },
            new() { Role = "user", Content = "Sản phẩm này có tốt không" },
            new() { Role = "model", Content = "Dạ tốt ạ" },
            new() { Role = "user", Content = "Ok" },
            new() { Role = "model", Content = "Dạ" },
            new() { Role = "user", Content = "K" }
        };

        // Act
        var context = await _analyzer.AnalyzeAsync(history);

        // Assert
        Assert.True(context.CurrentStage == JourneyStage.Stalled ||
                    context.Patterns.Any(p => p.Type == PatternType.EngagementDrop));
    }

    [Fact]
    public async Task AnalyzeAsync_PerformanceTest_CompletesUnder50ms()
    {
        // Arrange - Create 10 messages
        var history = new List<ConversationMessage>();
        for (int i = 0; i < 10; i++)
        {
            history.Add(new ConversationMessage
            {
                Role = "user",
                Content = "Cho em xem sản phẩm kem dưỡng da tốt nhất"
            });
            history.Add(new ConversationMessage
            {
                Role = "model",
                Content = "Dạ đây ạ chị"
            });
        }

        // Act
        var startTime = DateTime.UtcNow;
        var context = await _analyzer.AnalyzeAsync(history);
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        Assert.True(duration < 50, $"Analysis took {duration}ms, expected < 50ms");
    }
}
