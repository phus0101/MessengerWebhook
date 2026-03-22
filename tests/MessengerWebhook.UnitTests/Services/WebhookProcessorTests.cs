using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.StateMachine;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class WebhookProcessorTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<IMessengerService> _messengerServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<WebhookProcessor>> _loggerMock;
    private readonly WebhookProcessor _processor;

    public WebhookProcessorTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _messengerServiceMock = new Mock<IMessengerService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<WebhookProcessor>>();

        // Setup default state machine response
        _stateMachineMock
            .Setup(x => x.ProcessMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync("State machine response");

        _processor = new WebhookProcessor(
            _cache,
            _messengerServiceMock.Object,
            _stateMachineMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessMessage_ValidText_LogsCorrectly()
    {
        // Arrange
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: new Message("mid.123", "Hello World", null),
            Postback: null
        );

        // Act
        await _processor.ProcessAsync(messagingEvent);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from sender123")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_DuplicateId_SkipsProcessing()
    {
        // Arrange
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: new Message("mid.duplicate", "First message", null),
            Postback: null
        );

        // Act - Process first time
        await _processor.ProcessAsync(messagingEvent);

        // Reset mock to clear first call
        _loggerMock.Invocations.Clear();

        // Act - Process duplicate
        await _processor.ProcessAsync(messagingEvent);

        // Assert - Should log duplicate message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Duplicate message ignored")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Should NOT log "Processing message" again
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing message from")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessage_CachesMessageId_With48HourTTL()
    {
        // Arrange
        var messageId = "mid.cache-test";
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: new Message(messageId, "Test message", null),
            Postback: null
        );

        // Act
        await _processor.ProcessAsync(messagingEvent);

        // Assert - Cache should contain the message ID
        var cacheKey = $"msg:{messageId}";
        var exists = _cache.TryGetValue(cacheKey, out _);
        Assert.True(exists, "Message ID should be cached after processing");
    }

    [Fact]
    public async Task ProcessPostback_ValidPayload_ProcessesCorrectly()
    {
        // Arrange
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender789"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: null,
            Postback: new Postback("Get Started", "GET_STARTED_PAYLOAD")
        );

        // Act
        await _processor.ProcessAsync(messagingEvent);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Processing postback from sender789") &&
                    v.ToString()!.Contains("GET_STARTED_PAYLOAD")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_UnknownEventType_LogsWarning()
    {
        // Arrange - Event with no message or postback
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: null,
            Postback: null
        );

        // Act
        await _processor.ProcessAsync(messagingEvent);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown event type received")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_NullText_HandlesGracefully()
    {
        // Arrange
        var messagingEvent = new MessagingEvent(
            Sender: new Sender("sender123"),
            Recipient: new Recipient("page456"),
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message: new Message("mid.no-text", null, null),
            Postback: null
        );

        // Act
        await _processor.ProcessAsync(messagingEvent);

        // Assert - Should log with "[no text]" placeholder
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[no text]")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
