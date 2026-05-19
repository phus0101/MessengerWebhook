# Phase 3: Webhook Event Endpoint

## Context Links
- [Facebook Messenger API](../reports/researcher-260318-1431-facebook-messenger-api.md) - Section: Webhook Event Handling

## Overview
- **Priority:** P0 (Critical)
- **Status:** Pending
- **Mô tả:** Implement POST /webhook endpoint nhận events từ Facebook, parse payload, return 200 ngay

## Key Insights
- Facebook timeout sau 20 giây
- Best practice: Return 200 trong < 100ms, xử lý async
- Payload: object="page", entry[], messaging[]
- Event types: messages, message_echoes, messaging_postbacks

## Requirements

**Functional:**
- Endpoint POST /webhook nhận JSON payload
- Validate object === "page"
- Parse entry[] và messaging[]
- Return 200 OK ngay (không chờ processing)
- Queue events cho background processing

**Non-Functional:**
- Response time < 100ms (P95)
- Handle concurrent requests
- Graceful error handling

## Architecture

**Request Flow:**
```
Facebook → POST /webhook
         ↓
    Validate signature (Phase 4)
         ↓
    Parse payload → WebhookEvent
         ↓
    Enqueue to Channel
         ↓
    Return 200 OK
         ↓
    Background processing (Phase 5)
```

**Data Models:**
```csharp
public record WebhookEvent(string Object, Entry[] Entry);
public record Entry(string Id, long Time, MessagingEvent[] Messaging);
public record MessagingEvent(Sender Sender, Recipient Recipient, long Timestamp, Message? Message, Postback? Postback);
public record Message(string Mid, string? Text, Attachment[]? Attachments);
```

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Models/WebhookEvent.cs`
- `src/MessengerWebhook/Models/Entry.cs`
- `src/MessengerWebhook/Models/MessagingEvent.cs`
- `src/MessengerWebhook/Models/Message.cs`
- `src/MessengerWebhook/Models/Postback.cs`
- `src/MessengerWebhook/Models/Attachment.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`

## Implementation Steps

1. **Tạo model classes**
- WebhookEvent (root)
- Entry, MessagingEvent
- Message, Postback, Attachment
- Dùng record types cho immutability
- Nullable properties cho optional fields

2. **Setup Channel**
```csharp
builder.Services.AddSingleton(Channel.CreateBounded<MessagingEvent>(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));
```

3. **Implement POST /webhook**
```csharp
app.MapPost("/webhook", async (
    WebhookEvent webhookEvent,
    Channel<MessagingEvent> channel,
    ILogger<Program> logger) =>
{
    if (webhookEvent.Object != "page")
    {
        logger.LogWarning("Invalid object type: {Object}", webhookEvent.Object);
        return Results.NotFound();
    }

    var eventCount = 0;
    foreach (var entry in webhookEvent.Entry)
    {
        foreach (var messagingEvent in entry.Messaging)
        {
            await channel.Writer.WriteAsync(messagingEvent);
            eventCount++;
        }
    }

    logger.LogInformation("Webhook received: {EventCount} events queued", eventCount);
    return Results.Ok(new { status = "EVENT_RECEIVED" });
});
```

4. **Add request logging middleware**
```csharp
public class RequestLoggingMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        _logger.LogInformation(
            "Request {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            stopwatch.ElapsedMilliseconds,
            context.Response.StatusCode);
    }
}
```

5. **Write unit tests**
- Deserialize_ValidPayload_Success
- Deserialize_MessageEvent_ParsesCorrectly
- Deserialize_PostbackEvent_ParsesCorrectly

6. **Write integration test**
```csharp
[Fact]
public async Task PostWebhook_ValidPayload_Returns200AndQueuesEvent()
{
    var payload = new WebhookEvent("page", new[] { ... });
    var response = await client.PostAsJsonAsync("/webhook", payload);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

7. **Test với sample payload**
```bash
curl -X POST http://localhost:5000/webhook \
  -H "Content-Type: application/json" \
  -d '{"object":"page","entry":[{"id":"PAGE_ID","time":1234567890,"messaging":[{"sender":{"id":"USER_ID"},"recipient":{"id":"PAGE_ID"},"timestamp":1234567890,"message":{"mid":"MESSAGE_ID","text":"Hello"}}]}]}'
```

## Todo List
- [ ] Tạo model classes (WebhookEvent, Entry, MessagingEvent, Message, Postback, Attachment)
- [ ] Setup Channel<MessagingEvent>
- [ ] Implement POST /webhook endpoint
- [ ] Implement RequestLoggingMiddleware
- [ ] Write unit tests cho JSON deserialization
- [ ] Write integration test
- [ ] Test với sample payload
- [ ] Measure response time (< 100ms)

## Success Criteria
- Valid payload → 200 OK
- Response time < 100ms (P95)
- Events enqueued thành công
- Invalid object → 404
- Malformed JSON → 400
- All tests pass

## Risk Assessment
- **Risk:** Channel full khi high traffic
  - **Mitigation:** BoundedChannelFullMode.Wait, monitor queue depth
- **Risk:** JSON deserialization lỗi
  - **Mitigation:** Nullable properties, ignore unknown fields
- **Risk:** Response time > 100ms
  - **Mitigation:** Không xử lý logic trong endpoint

## Security Considerations
- Chưa validate signature (Phase 4)
- Log payload nhưng sanitize PII
- Rate limiting (implement sau)

## Next Steps
- Phase 4: Signature validation middleware
