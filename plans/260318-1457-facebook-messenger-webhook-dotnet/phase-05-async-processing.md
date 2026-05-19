# Phase 5: Async Processing

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Async Processing Patterns

## Overview
- **Priority:** P0 (Critical)
- **Status:** Pending
- **Mô tả:** Implement BackgroundService với Channel để xử lý webhook events bất đồng bộ

## Key Insights
- Channel-based processing: lightweight, in-process, no external dependencies
- BackgroundService runs continuously, reads from Channel
- Target: < 5s processing latency
- BoundedChannelFullMode.Wait cho backpressure

## Requirements

**Functional:**
- BackgroundService đọc events từ Channel
- Process message events (text, attachments)
- Process postback events
- Handle errors gracefully
- Idempotency check (duplicate message detection)

**Non-Functional:**
- Processing latency < 5s (P95)
- Graceful shutdown
- No message loss
- Monitor queue depth

## Architecture

**Processing Flow:**
```
Channel Queue → BackgroundService
              ↓
    Read MessagingEvent
              ↓
    Idempotency check (message ID)
              ↓
    Route by event type
              ↓
    Process message/postback
              ↓
    Call Graph API (Phase 6)
              ↓
    Log completion
```

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Services/WebhookProcessor.cs`
- `src/MessengerWebhook/BackgroundServices/WebhookProcessingService.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`

## Implementation Steps

1. **Tạo WebhookProcessor service**
```csharp
public class WebhookProcessor
{
    private readonly IMessengerService _messengerService;
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IMemoryCache _processedMessages;

    public async Task ProcessAsync(MessagingEvent messagingEvent)
    {
        // Idempotency check
        if (messagingEvent.Message != null)
        {
            var messageId = messagingEvent.Message.Mid;
            if (_processedMessages.TryGetValue(messageId, out _))
            {
                _logger.LogInformation("Duplicate message ignored: {MessageId}", messageId);
                return;
            }

            await ProcessMessageAsync(messagingEvent);
            _processedMessages.Set(messageId, true, TimeSpan.FromHours(48));
        }
        else if (messagingEvent.Postback != null)
        {
            await ProcessPostbackAsync(messagingEvent);
        }
    }

    private async Task ProcessMessageAsync(MessagingEvent evt)
    {
        var senderId = evt.Sender.Id;
        var text = evt.Message?.Text;

        if (!string.IsNullOrEmpty(text))
        {
            // Echo back for now
            await _messengerService.SendTextMessageAsync(senderId, $"You said: {text}");
        }
    }

    private async Task ProcessPostbackAsync(MessagingEvent evt)
    {
        var senderId = evt.Sender.Id;
        var payload = evt.Postback?.Payload;

        _logger.LogInformation("Postback received: {Payload}", payload);
        await _messengerService.SendTextMessageAsync(senderId, "Postback received");
    }
}
```

2. **Implement WebhookProcessingService**
```csharp
public class WebhookProcessingService : BackgroundService
{
    private readonly Channel<MessagingEvent> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookProcessingService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook processing service started");

        await foreach (var messagingEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();

                var stopwatch = Stopwatch.StartNew();
                await processor.ProcessAsync(messagingEvent);
                stopwatch.Stop();

                _logger.LogInformation(
                    "Event processed in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook event");
                // Don't throw - continue processing next events
            }
        }

        _logger.LogInformation("Webhook processing service stopped");
    }
}
```

3. **Register services**
```csharp
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WebhookProcessor>();
builder.Services.AddHostedService<WebhookProcessingService>();
```

4. **Add queue depth monitoring**
```csharp
app.MapGet("/metrics/queue-depth", (Channel<MessagingEvent> channel) =>
{
    var count = channel.Reader.Count;
    return Results.Ok(new { queueDepth = count });
});
```

5. **Write unit tests**
- ProcessMessage_ValidText_SendsEcho
- ProcessMessage_DuplicateId_Skips
- ProcessPostback_ValidPayload_Processes

6. **Write integration test**
```csharp
[Fact]
public async Task BackgroundService_ProcessesQueuedEvents()
{
    // Arrange
    var channel = Channel.CreateUnbounded<MessagingEvent>();
    var evt = new MessagingEvent(...);

    // Act
    await channel.Writer.WriteAsync(evt);
    await Task.Delay(1000); // Wait for processing

    // Assert
    // Verify message was processed (check logs or mock)
}
```

## Todo List
- [ ] Tạo WebhookProcessor service
- [ ] Implement WebhookProcessingService
- [ ] Add MemoryCache cho idempotency
- [ ] Register services và BackgroundService
- [ ] Add queue depth monitoring endpoint
- [ ] Write unit tests
- [ ] Write integration test
- [ ] Test graceful shutdown

## Success Criteria
- Events processed từ Channel
- Processing latency < 5s
- Duplicate messages skipped
- Graceful shutdown không mất messages
- All tests pass
- Queue depth monitoring works

## Risk Assessment
- **Risk:** Message loss khi app restart
  - **Mitigation:** Document limitation, consider persistent queue sau
- **Risk:** Processing bottleneck
  - **Mitigation:** Monitor queue depth, scale horizontally

## Security Considerations
- Sanitize user input trước khi log
- Rate limit per user (implement sau)
- Validate message content

## Next Steps
- Phase 6: Facebook Graph API integration
