using MessengerWebhook.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services;

/// <summary>
/// Processes webhook events with idempotency check
/// </summary>
public class WebhookProcessor
{
    private readonly IMemoryCache _cache;
    private readonly IMessengerService _messengerService;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IMemoryCache cache,
        IMessengerService messengerService,
        ILogger<WebhookProcessor> logger)
    {
        _cache = cache;
        _messengerService = messengerService;
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

        _logger.LogInformation(
            "Processing message from {SenderId}: {Text}",
            senderId,
            text ?? "[no text]");

        // Send echo reply
        if (!string.IsNullOrEmpty(text))
        {
            var reply = $"Bạn đã nói: {text}";
            await _messengerService.SendTextMessageAsync(senderId, reply);
            _logger.LogInformation("Reply sent to {SenderId}", senderId);
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

        // Send postback acknowledgment
        var reply = $"Đã nhận postback: {payload}";
        await _messengerService.SendTextMessageAsync(senderId, reply);
        _logger.LogInformation("Postback reply sent to {SenderId}", senderId);

        // Mark as processed (48h TTL with size tracking)
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48),
            Size = 1
        });
    }
}
