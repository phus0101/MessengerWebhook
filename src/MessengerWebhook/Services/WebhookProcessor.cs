using MessengerWebhook.Models;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.StateMachine;
using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services;

/// <summary>
/// Processes webhook events with idempotency check
/// </summary>
public class WebhookProcessor
{
    private readonly IMemoryCache _cache;
    private readonly IMessengerService _messengerService;
    private readonly IStateMachine _stateMachine;
    private readonly IQuickReplyHandler _quickReplyHandler;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IMemoryCache cache,
        IMessengerService messengerService,
        IStateMachine stateMachine,
        IQuickReplyHandler quickReplyHandler,
        ILogger<WebhookProcessor> logger)
    {
        _cache = cache;
        _messengerService = messengerService;
        _stateMachine = stateMachine;
        _quickReplyHandler = quickReplyHandler;
        _logger = logger;
    }

    public async Task ProcessAsync(MessagingEvent messagingEvent)
    {
        if (messagingEvent.Message != null)
        {
            await ProcessMessageAsync(messagingEvent);
        }
        else if (messagingEvent.Postback != null)
        {
            await ProcessPostbackAsync(messagingEvent);
        }
        else
        {
            _logger.LogWarning("Unknown event type received");
        }
    }

    private async Task ProcessMessageAsync(MessagingEvent evt)
    {
        var messageId = evt.Message!.Mid;

        // Idempotency check
        var cacheKey = $"msg:{messageId}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogInformation("Duplicate message ignored: {MessageId}", messageId);
            return;
        }

        var senderId = evt.Sender.Id;
        var text = evt.Message.Text;

        // Check for Quick Reply
        if (evt.Message.QuickReply != null)
        {
            _logger.LogInformation(
                "Processing Quick Reply from {SenderId}: {Payload}",
                senderId,
                evt.Message.QuickReply.Payload);

            try
            {
                var reply = await _quickReplyHandler.HandleQuickReplyAsync(senderId, evt.Message.QuickReply.Payload);
                await _messengerService.SendTextMessageAsync(senderId, reply);
                _logger.LogInformation("Quick Reply response sent to {SenderId}", senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Quick Reply for {SenderId}", senderId);
                await _messengerService.SendTextMessageAsync(
                    senderId,
                    "Xin lỗi, em đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.");
            }
        }
        else if (!string.IsNullOrEmpty(text))
        {
            _logger.LogInformation(
                "Processing message from {SenderId}: {Text}",
                senderId,
                text);

            // Process message through state machine
            try
            {
                var reply = await _stateMachine.ProcessMessageAsync(senderId, text);
                await _messengerService.SendTextMessageAsync(senderId, reply);
                _logger.LogInformation("State machine reply sent to {SenderId}", senderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message for {SenderId}", senderId);

                // Fallback to simple acknowledgment
                await _messengerService.SendTextMessageAsync(
                    senderId,
                    "Xin lỗi, tôi đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.");
            }
        }

        // Mark as processed (48h TTL with size tracking)
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48),
            Size = 1
        });
    }

    private async Task ProcessPostbackAsync(MessagingEvent evt)
    {
        // Idempotency check using timestamp + sender + payload
        var cacheKey = $"postback:{evt.Sender.Id}:{evt.Timestamp}:{evt.Postback!.Payload}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogInformation("Duplicate postback ignored: {SenderId}:{Payload}",
                evt.Sender.Id, evt.Postback.Payload);
            return;
        }

        var senderId = evt.Sender.Id;
        var payload = evt.Postback.Payload;

        _logger.LogInformation(
            "Processing postback from {SenderId}: {Payload}",
            senderId,
            payload);

        try
        {
            var reply = await _quickReplyHandler.HandlePostbackAsync(senderId, payload);
            await _messengerService.SendTextMessageAsync(senderId, reply);
            _logger.LogInformation("Postback response sent to {SenderId}", senderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Postback for {SenderId}", senderId);
            await _messengerService.SendTextMessageAsync(
                senderId,
                "Xin lỗi, em đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.");
        }

        // Mark as processed (48h TTL with size tracking)
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48),
            Size = 1
        });
    }
}
