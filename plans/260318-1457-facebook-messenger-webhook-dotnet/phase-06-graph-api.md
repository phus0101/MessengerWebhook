# Phase 6: Facebook Graph API Integration

## Context Links
- [Facebook Messenger API](../reports/researcher-260318-1431-facebook-messenger-api.md) - Section: Message Response Formats
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Error Handling & Retry Mechanisms

## Overview
- **Priority:** P0 (Critical)
- **Status:** Pending
- **Mô tả:** Implement MessengerService để gọi Facebook Graph API Send API với Polly retry policies

## Key Insights
- Send API endpoint: `https://graph.facebook.com/v21.0/me/messages`
- Requires Page Access Token
- Support text messages, templates, quick replies
- Rate limit: ~1 call/second safe
- Retry với exponential backoff cho 429/500 errors

## Requirements

**Functional:**
- Send text messages
- Send quick replies
- Send generic templates (carousel)
- Handle API errors
- Retry với Polly policies

**Non-Functional:**
- Resilient to transient failures
- Exponential backoff cho rate limits
- Circuit breaker cho sustained failures
- Timeout 10s per request

## Architecture

**API Call Flow:**
```
MessengerService → HttpClient
                 ↓
    Polly Retry Policy (3 attempts)
                 ↓
    POST /v21.0/me/messages
                 ↓
    Handle 200/400/429/500
                 ↓
    Return success/failure
```

**Polly Policies:**
- Retry: 3 attempts, exponential backoff
- Circuit Breaker: Open after 5 consecutive failures
- Timeout: 10s per request

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Services/IMessengerService.cs`
- `src/MessengerWebhook/Services/MessengerService.cs`
- `src/MessengerWebhook/Models/SendMessageRequest.cs`
- `src/MessengerWebhook/Models/SendMessageResponse.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/Configuration/FacebookOptions.cs`

## Implementation Steps

1. **Tạo IMessengerService interface**
```csharp
public interface IMessengerService
{
    Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text);
    Task<SendMessageResponse> SendQuickRepliesAsync(string recipientId, string text, QuickReply[] replies);
    Task<SendMessageResponse> SendGenericTemplateAsync(string recipientId, GenericElement[] elements);
}
```

2. **Tạo request/response models**
```csharp
public record SendMessageRequest(
    Recipient Recipient,
    MessageContent Message
);

public record Recipient(string Id);

public record MessageContent(
    string? Text,
    QuickReply[]? QuickReplies,
    Attachment? Attachment
);

public record SendMessageResponse(
    string RecipientId,
    string MessageId
);
```

3. **Implement MessengerService**
```csharp
public class MessengerService : IMessengerService
{
    private readonly HttpClient _httpClient;
    private readonly FacebookOptions _options;
    private readonly ILogger<MessengerService> _logger;

    public MessengerService(HttpClient httpClient, IOptions<FacebookOptions> options, ILogger<MessengerService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text)
    {
        var request = new SendMessageRequest(
            new Recipient(recipientId),
            new MessageContent(text, null, null)
        );

        return await SendAsync(request);
    }

    private async Task<SendMessageResponse> SendAsync(SendMessageRequest request)
    {
        var url = $"{_options.GraphApiBaseUrl}/{_options.ApiVersion}/me/messages?access_token={_options.PageAccessToken}";

        var response = await _httpClient.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Graph API error: {StatusCode} - {Error}", response.StatusCode, error);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException("Rate limit exceeded", null, response.StatusCode);
            }

            throw new HttpRequestException($"Graph API error: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<SendMessageResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }
}
```

4. **Configure HttpClient với Polly**
```csharp
builder.Services.AddHttpClient<IMessengerService, MessengerService>()
    .AddResilienceHandler("messenger-pipeline", builder =>
    {
        // Retry policy
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    || r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
        });

        // Circuit breaker
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30)
        });

        // Timeout
        builder.AddTimeout(TimeSpan.FromSeconds(10));
    });
```

5. **Write unit tests**
- SendTextMessage_ValidRequest_ReturnsSuccess
- SendTextMessage_RateLimited_RetriesAndSucceeds
- SendTextMessage_ServerError_RetriesAndFails
- SendTextMessage_CircuitOpen_ThrowsException

6. **Write integration test với mock HTTP**
```csharp
[Fact]
public async Task SendTextMessage_ValidRequest_CallsGraphApi()
{
    // Arrange
    var mockHandler = new MockHttpMessageHandler();
    mockHandler.When("https://graph.facebook.com/*/me/messages*")
        .Respond("application/json", "{\"recipient_id\":\"123\",\"message_id\":\"mid.456\"}");

    var httpClient = mockHandler.ToHttpClient();
    var service = new MessengerService(httpClient, options, logger);

    // Act
    var result = await service.SendTextMessageAsync("123", "Hello");

    // Assert
    result.RecipientId.Should().Be("123");
    result.MessageId.Should().Be("mid.456");
}
```

## Todo List
- [ ] Tạo IMessengerService interface
- [ ] Tạo request/response models
- [ ] Implement MessengerService
- [ ] Configure HttpClient với Polly policies
- [ ] Add PageAccessToken vào FacebookOptions
- [ ] Write unit tests
- [ ] Write integration test với mock HTTP
- [ ] Test với real Facebook API (manual)

## Success Criteria
- Text messages gửi thành công
- Rate limit errors được retry
- Server errors được retry
- Circuit breaker opens sau sustained failures
- All tests pass
- Real API call works

## Risk Assessment
- **Risk:** Token expiration
  - **Mitigation:** Document token refresh process
- **Risk:** Rate limit exhaustion
  - **Mitigation:** Monitor API call rate, implement queuing

## Security Considerations
- Store Page Access Token securely
- Never log access tokens
- Validate recipient IDs
- Sanitize message content

## Next Steps
- Phase 7: Logging & monitoring
