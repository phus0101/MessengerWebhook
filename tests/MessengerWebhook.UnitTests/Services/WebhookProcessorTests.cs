using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.StateMachine;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.Services;

public class WebhookProcessorTests
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly Mock<IMessengerService> _messengerService = new();
    private readonly Mock<IStateMachine> _stateMachine = new();
    private readonly Mock<IQuickReplyHandler> _quickReplyHandler = new();
    private readonly WebhookProcessor _processor;

    public WebhookProcessorTests()
    {
        _stateMachine
            .Setup(x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("State machine response");
        _quickReplyHandler
            .Setup(x => x.HandlePostbackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("Postback response");

        _processor = new WebhookProcessor(
            _cache,
            _messengerService.Object,
            _stateMachine.Object,
            _quickReplyHandler.Object,
            Mock.Of<ILogger<WebhookProcessor>>());
    }

    [Fact]
    public async Task ProcessMessage_ValidText_CallsStateMachineAndMessenger()
    {
        var messagingEvent = new MessagingEvent(
            new Sender("sender123"),
            new Recipient("page456"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new Message("mid.123", "Hello World", null, null),
            null);

        await _processor.ProcessAsync(messagingEvent);

        _stateMachine.Verify(x => x.ProcessMessageAsync("sender123", "Hello World", "page456"), Times.Once);
        _messengerService.Verify(x => x.SendTextMessageAsync("sender123", "State machine response", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_DuplicateId_SkipsProcessing()
    {
        var messagingEvent = new MessagingEvent(
            new Sender("sender123"),
            new Recipient("page456"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new Message("mid.duplicate", "First message", null, null),
            null);

        await _processor.ProcessAsync(messagingEvent);
        await _processor.ProcessAsync(messagingEvent);

        _stateMachine.Verify(x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPostback_ValidPayload_UsesQuickReplyHandler()
    {
        var messagingEvent = new MessagingEvent(
            new Sender("sender789"),
            new Recipient("page456"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            null,
            new Postback("Get Started", "GET_STARTED_PAYLOAD"));

        _quickReplyHandler.Setup(x => x.HandlePostbackAsync("sender789", "GET_STARTED_PAYLOAD", "page456"))
            .ReturnsAsync("Postback response");

        await _processor.ProcessAsync(messagingEvent);

        _quickReplyHandler.Verify(x => x.HandlePostbackAsync("sender789", "GET_STARTED_PAYLOAD", "page456"), Times.Once);
        _messengerService.Verify(x => x.SendTextMessageAsync("sender789", "Postback response", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_UnknownEventType_DoesNothingDangerous()
    {
        var messagingEvent = new MessagingEvent(
            new Sender("sender123"),
            new Recipient("page456"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            null,
            null);

        await _processor.ProcessAsync(messagingEvent);

        _messengerService.Verify(x => x.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessage_NullText_HandlesGracefully()
    {
        var messagingEvent = new MessagingEvent(
            new Sender("sender123"),
            new Recipient("page456"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new Message("mid.no-text", null, null, null),
            null);

        await _processor.ProcessAsync(messagingEvent);

        _stateMachine.Verify(x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _messengerService.Verify(x => x.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
