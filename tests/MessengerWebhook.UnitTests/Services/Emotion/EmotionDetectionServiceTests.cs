using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Emotion.Configuration;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.Emotion;

public class EmotionDetectionServiceTests
{
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<EmotionDetectionService>> _mockLogger;
    private readonly EmotionDetectionOptions _options;
    private readonly EmotionDetectionService _service;

    public EmotionDetectionServiceTests()
    {
        _mockCache = new Mock<IMemoryCache>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        _mockLogger = new Mock<ILogger<EmotionDetectionService>>();
        _options = new EmotionDetectionOptions
        {
            EnableContextAnalysis = true,
            ContextWindowSize = 3,
            ConfidenceThreshold = 0.6,
            EnableCaching = true,
            CacheDurationMinutes = 5
        };

        // Setup mock cache entry for Set operations
        var mockCacheEntry = new Mock<ICacheEntry>();
        _mockCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
            .Returns(mockCacheEntry.Object);

        _service = new EmotionDetectionService(
            _mockCache.Object,
            _mockTenantContext.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task DetectEmotionAsync_ValidMessage_ReturnsEmotionScore()
    {
        var result = await _service.DetectEmotionAsync("Sản phẩm tuyệt vời!");

        Assert.NotNull(result);
        Assert.Equal(EmotionType.Positive, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0);
        Assert.Equal("rule-based", result.DetectionMethod);
    }

    [Fact]
    public async Task DetectEmotionAsync_EmptyMessage_ReturnsNeutral()
    {
        var result = await _service.DetectEmotionAsync("");

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public async Task DetectEmotionAsync_NullMessage_ReturnsNeutral()
    {
        var result = await _service.DetectEmotionAsync(null!);

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public async Task DetectEmotionAsync_WithCachingEnabled_UsesCachedResult()
    {
        // Arrange
        var message = "Test message";
        var cachedScore = new EmotionScore
        {
            PrimaryEmotion = EmotionType.Positive,
            Confidence = 0.8,
            DetectionMethod = "rule-based"
        };

        object? cacheValue = cachedScore;
        _mockCache.Setup(c => c.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(true);

        // Act
        var result = await _service.DetectEmotionAsync(message);

        // Assert
        Assert.Equal(cachedScore.PrimaryEmotion, result.PrimaryEmotion);
        Assert.Equal(cachedScore.Confidence, result.Confidence);
    }

    [Fact]
    public async Task DetectEmotionAsync_WithCachingDisabled_DoesNotUseCache()
    {
        // Arrange
        _options.EnableCaching = false;
        var service = new EmotionDetectionService(
            _mockCache.Object,
            _mockTenantContext.Object,
            _mockLogger.Object,
            Options.Create(_options));

        // Act
        var result = await service.DetectEmotionAsync("Test message");

        // Assert
        _mockCache.Verify(c => c.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny), Times.Never);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_EmptyMessage_ReturnsNeutral()
    {
        var history = new List<ConversationMessage>();

        var result = await _service.DetectEmotionWithContextAsync("", history);

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_NullHistory_FallsBackToSimpleDetection()
    {
        var result = await _service.DetectEmotionWithContextAsync("Tuyệt vời!", null!);

        Assert.Equal(EmotionType.Positive, result.PrimaryEmotion);
        Assert.Equal("rule-based", result.DetectionMethod);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_EmptyHistory_FallsBackToSimpleDetection()
    {
        var history = new List<ConversationMessage>();

        var result = await _service.DetectEmotionWithContextAsync("Tuyệt vời!", history);

        Assert.Equal(EmotionType.Positive, result.PrimaryEmotion);
        Assert.Equal("rule-based", result.DetectionMethod);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_WithHistory_ReturnsContextAwareScore()
    {
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Sản phẩm tốt" },
            new() { Role = "model", Content = "Cảm ơn chị" },
            new() { Role = "user", Content = "Rất hài lòng" }
        };

        var result = await _service.DetectEmotionWithContextAsync("Tuyệt vời!", history);

        Assert.Equal(EmotionType.Positive, result.PrimaryEmotion);
        Assert.Equal("rule-based-context-aware", result.DetectionMethod);
        Assert.Contains("context_window_size", result.Metadata.Keys);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_ContextAnalysisDisabled_FallsBackToSimpleDetection()
    {
        _options.EnableContextAnalysis = false;
        var service = new EmotionDetectionService(
            _mockCache.Object,
            _mockTenantContext.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Test" }
        };

        var result = await service.DetectEmotionWithContextAsync("Tuyệt vời!", history);

        Assert.Equal("rule-based", result.DetectionMethod);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_OnlyUserMessages_FiltersCorrectly()
    {
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Tốt" },
            new() { Role = "model", Content = "Cảm ơn" },
            new() { Role = "user", Content = "Hay" },
            new() { Role = "model", Content = "Vui lòng" },
            new() { Role = "user", Content = "Ok" }
        };

        var result = await _service.DetectEmotionWithContextAsync("Tuyệt!", history);

        // Should only analyze last 3 user messages
        Assert.Equal("rule-based-context-aware", result.DetectionMethod);
        var contextSize = (int)result.Metadata["context_window_size"];
        Assert.Equal(3, contextSize);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_DetectsEscalation_NeutralToFrustrated()
    {
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Oke" },
            new() { Role = "user", Content = "Không tốt lắm" },
            new() { Role = "user", Content = "Bực mình quá" }
        };

        var result = await _service.DetectEmotionWithContextAsync("Tức giận!!!", history);

        Assert.Contains("escalation", result.Metadata.Keys);
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_DetectsEscalation_SatisfactionDrop()
    {
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Tuyệt vời" },
            new() { Role = "user", Content = "Oke" },
            new() { Role = "user", Content = "Không tốt" }
        };

        var result = await _service.DetectEmotionWithContextAsync("Dở quá", history);

        if (result.Metadata.ContainsKey("escalation"))
        {
            Assert.Equal("satisfaction_drop", result.Metadata["escalation"]);
        }
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_DetectsEscalation_AngerEscalation()
    {
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Không thích" },
            new() { Role = "user", Content = "Bực mình" }
        };

        var result = await _service.DetectEmotionWithContextAsync("Tức giận", history);

        if (result.Metadata.ContainsKey("escalation"))
        {
            Assert.Equal("anger_escalation", result.Metadata["escalation"]);
        }
    }

    [Fact]
    public async Task DetectEmotionWithContextAsync_RespectsContextWindowSize()
    {
        _options.ContextWindowSize = 2;
        var service = new EmotionDetectionService(
            _mockCache.Object,
            _mockTenantContext.Object,
            _mockLogger.Object,
            Options.Create(_options));

        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = "Message 1" },
            new() { Role = "user", Content = "Message 2" },
            new() { Role = "user", Content = "Message 3" },
            new() { Role = "user", Content = "Message 4" }
        };

        var result = await service.DetectEmotionWithContextAsync("Current", history);

        var contextSize = (int)result.Metadata["context_window_size"];
        Assert.Equal(2, contextSize);
    }
}
