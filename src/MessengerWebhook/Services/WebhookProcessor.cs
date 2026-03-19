using MessengerWebhook.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services;

/// <summary>
/// Processes webhook events with idempotency check
/// </summary>
public class WebhookProcessor
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(IMemoryCache cache, ILogger<WebhookProcessor> logger)
    {
        _cache = cache;
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

        // TODO: Phase 6 - Call Graph API to send reply
        // For now, just log
        await Task.CompletedTask;

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

        // TODO: Phase 6 - Call Graph API to send reply
        await Task.CompletedTask;

        // Mark as processed (48h TTL with size tracking)
        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48),
            Size = 1
        });
    }
}
