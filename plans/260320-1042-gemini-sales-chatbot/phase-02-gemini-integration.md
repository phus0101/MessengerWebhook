# Phase 2: Gemini Integration

**Priority**: Critical
**Status**: Pending
**Duration**: 1 week
**Dependencies**: Phase 1 (Database Setup)

---

## Context Links

- Research: [Gemini API Report](../reports/researcher-260320-1042-gemini-api.md)
- Current Code: `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\MessengerService.cs`

---

## Overview

Integrate Google Gemini Pro 3.1 API into ASP.NET Core webhook. Implement hybrid model strategy (Flash-Lite for simple queries, Pro for complex consultation), retry logic with exponential backoff, circuit breaker pattern, and cost optimization through context management.

---

## Key Insights

- Official Google Gen AI .NET SDK supports both Gemini Developer API and Vertex AI
- Stateless conversation pattern recommended (client manages history)
- Streaming responses improve perceived latency (~400ms first token)
- Hybrid model approach reduces costs by 60-70%
- Rate limits: RPM, RPD, TPM, IPM dimensions
- Context window: 65,000 output tokens per response
- Spend caps available (March 2026 feature)

---

## Requirements

### Functional
- Send text messages to Gemini API with conversation history
- Support streaming responses for real-time chat
- Implement model routing (Flash-Lite vs Pro based on complexity)
- Maintain conversation context across turns
- Handle Vietnamese language input/output
- Support system instructions for persona definition

### Non-Functional
- Response time: <1s first token (streaming)
- Availability: 99.5% with fallback strategies
- Cost: <$0.10 per conversation (10 turns avg)
- Rate limit handling: Exponential backoff with jitter
- Circuit breaker: Stop after 5 consecutive failures
- Timeout: 60s per API call

---

## Architecture

### Service Layer Structure
```
Services/
├── AI/
│   ├── IGeminiService.cs
│   ├── GeminiService.cs
│   ├── Models/
│   │   ├── GeminiRequest.cs
│   │   ├── GeminiResponse.cs
│   │   ├── ConversationMessage.cs
│   │   └── GeminiModelType.cs (enum: Pro, FlashLite)
│   ├── Handlers/
│   │   ├── GeminiAuthHandler.cs (DelegatingHandler)
│   │   └── GeminiRetryHandler.cs (DelegatingHandler)
│   └── Strategies/
│       ├── IModelSelectionStrategy.cs
│       └── HybridModelSelectionStrategy.cs
```

### Request Flow
```
User Message → Load Conversation History → Select Model (Pro/Flash-Lite)
    → Build Gemini Request → Send with Retry → Stream Response
    → Save to History → Return to User
```

### Model Selection Logic
```csharp
if (message.Contains("tư vấn") || message.Contains("gợi ý") || message.Length > 100)
    return GeminiModelType.Pro; // Complex consultation
else
    return GeminiModelType.FlashLite; // Simple queries
```

---

## Related Code Files

### To Create
- `src/MessengerWebhook/Services/AI/IGeminiService.cs`
- `src/MessengerWebhook/Services/AI/GeminiService.cs`
- `src/MessengerWebhook/Services/AI/Models/GeminiRequest.cs`
- `src/MessengerWebhook/Services/AI/Models/GeminiResponse.cs`
- `src/MessengerWebhook/Services/AI/Models/ConversationMessage.cs`
- `src/MessengerWebhook/Services/AI/Models/GeminiModelType.cs`
- `src/MessengerWebhook/Services/AI/Handlers/GeminiAuthHandler.cs`
- `src/MessengerWebhook/Services/AI/Handlers/GeminiRetryHandler.cs`
- `src/MessengerWebhook/Services/AI/Strategies/IModelSelectionStrategy.cs`
- `src/MessengerWebhook/Services/AI/Strategies/HybridModelSelectionStrategy.cs`
- `src/MessengerWebhook/Configuration/GeminiOptions.cs`

### To Modify
- `src/MessengerWebhook/Program.cs` (register Gemini services)
- `src/MessengerWebhook/appsettings.json` (add Gemini config)

---

## Implementation Steps

### 1. Install NuGet Packages
```bash
cd "D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook"
dotnet add package Google.Cloud.AIPlatform.V1
# OR community package:
# dotnet add package GenerativeAI
dotnet add package Polly
```

### 2. Create Configuration Model
```csharp
public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ProModel { get; set; } = "gemini-3.1-pro";
    public string FlashLiteModel { get; set; } = "gemini-3.1-flash-lite";
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 60;
    public RateLimitOptions RateLimits { get; set; } = new();
}

public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 60;
    public int TokensPerMinute { get; set; } = 100000;
}
```

### 3. Implement GeminiAuthHandler
```csharp
public class GeminiAuthHandler : DelegatingHandler
{
    private readonly GeminiOptions _options;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add API key to query string
        var uriBuilder = new UriBuilder(request.RequestUri!);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["key"] = _options.ApiKey;
        uriBuilder.Query = query.ToString();
        request.RequestUri = uriBuilder.Uri;

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### 4. Implement GeminiRetryHandler with Polly
```csharp
public class GeminiRetryHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public GeminiRetryHandler()
    {
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempt
                });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(
            () => base.SendAsync(request, cancellationToken));
    }
}
```

### 5. Implement IGeminiService Interface
```csharp
public interface IGeminiService
{
    Task<string> SendMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    GeminiModelType SelectModel(string message);
}
```

### 6. Implement GeminiService
```csharp
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IModelSelectionStrategy _modelStrategy;
    private readonly ILogger<GeminiService> _logger;

    public async Task<string> SendMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var model = modelOverride ?? _modelStrategy.SelectModel(message);
        var modelName = model == GeminiModelType.Pro
            ? _options.ProModel
            : _options.FlashLiteModel;

        // Build request
        var request = new
        {
            contents = BuildContents(message, history),
            systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } },
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = _options.MaxTokens
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API error: {StatusCode} - {Error}",
                response.StatusCode, error);
            throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken);
        return result?.Candidates?[0]?.Content?.Parts?[0]?.Text
            ?? throw new InvalidOperationException("Invalid response format");
    }

    private string GetSystemPrompt()
    {
        return @"Bạn là chuyên viên tư vấn thời trang chuyên nghiệp cho cửa hàng quần áo.
Nhiệm vụ của bạn:
- Giúp khách hàng tìm sản phẩm phù hợp với phong cách, dáng người, và dịp sử dụng
- Đặt câu hỏi làm rõ nhu cầu (tối đa 2 câu hỏi mỗi lần)
- Gợi ý sản phẩm cụ thể khi đã hiểu rõ nhu cầu
- Trả lời ngắn gọn, thân thiện, chuyên nghiệp
- Chỉ giới thiệu sản phẩm có trong danh mục của cửa hàng";
    }

    private object[] BuildContents(string message, List<ConversationMessage> history)
    {
        var contents = new List<object>();

        // Add history
        foreach (var msg in history)
        {
            contents.Add(new
            {
                role = msg.Role,
                parts = new[] { new { text = msg.Content } }
            });
        }

        // Add current message
        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = message } }
        });

        return contents.ToArray();
    }
}
```

### 7. Implement Model Selection Strategy
```csharp
public class HybridModelSelectionStrategy : IModelSelectionStrategy
{
    private readonly string[] _complexKeywords =
    {
        "tư vấn", "gợi ý", "phù hợp", "nên mặc", "đề xuất",
        "recommend", "suggest", "advice"
    };

    public GeminiModelType SelectModel(string message)
    {
        // Use Pro for complex consultation
        if (message.Length > 100 ||
            _complexKeywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return GeminiModelType.Pro;
        }

        // Use Flash-Lite for simple queries
        return GeminiModelType.FlashLite;
    }
}
```

### 8. Register Services in Program.cs
```csharp
// Configuration
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection("Gemini"));

// HttpClient with handlers
builder.Services.AddTransient<GeminiAuthHandler>();
builder.Services.AddTransient<GeminiRetryHandler>();

builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddHttpMessageHandler<GeminiAuthHandler>()
.AddHttpMessageHandler<GeminiRetryHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Strategies
builder.Services.AddSingleton<IModelSelectionStrategy, HybridModelSelectionStrategy>();
```

### 9. Add Configuration to appsettings.json
```json
{
  "Gemini": {
    "ApiKey": "", // Use User Secrets
    "ProModel": "gemini-3.1-pro",
    "FlashLiteModel": "gemini-3.1-flash-lite",
    "MaxTokens": 2048,
    "Temperature": 0.7,
    "MaxRetries": 3,
    "TimeoutSeconds": 60,
    "RateLimits": {
      "RequestsPerMinute": 60,
      "TokensPerMinute": 100000
    }
  }
}
```

### 10. Set API Key in User Secrets
```bash
cd "D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook"
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY_HERE"
```

### 11. Implement Circuit Breaker (Optional)
```csharp
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1),
        onBreak: (exception, duration) =>
        {
            // Log circuit breaker opened
        },
        onReset: () =>
        {
            // Log circuit breaker reset
        });
```

### 12. Write Unit Tests
```csharp
[Fact]
public async Task SendMessageAsync_ValidRequest_ReturnsResponse()
{
    // Arrange
    var mockHandler = new MockHttpMessageHandler();
    var service = new GeminiService(mockHandler.ToHttpClient(), options, strategy, logger);

    // Act
    var result = await service.SendMessageAsync("user123", "Xin chào", new List<ConversationMessage>());

    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result);
}

[Fact]
public void SelectModel_ComplexQuery_ReturnsPro()
{
    // Arrange
    var strategy = new HybridModelSelectionStrategy();

    // Act
    var model = strategy.SelectModel("Tôi cần tư vấn trang phục phù hợp cho dự tiệc");

    // Assert
    Assert.Equal(GeminiModelType.Pro, model);
}
```

### 13. Integration Testing
Test with real Gemini API:
- Simple greeting (Flash-Lite)
- Complex consultation (Pro)
- Multi-turn conversation
- Error handling (invalid API key, rate limit)
- Vietnamese language support

---

## Todo List

- [ ] Install Google Gen AI SDK and Polly packages
- [ ] Create GeminiOptions configuration model
- [ ] Implement GeminiAuthHandler for API key injection
- [ ] Implement GeminiRetryHandler with exponential backoff
- [ ] Create IGeminiService interface
- [ ] Implement GeminiService with streaming support
- [ ] Create model selection strategy (hybrid approach)
- [ ] Register services in DI container
- [ ] Add Gemini configuration to appsettings.json
- [ ] Set API key in User Secrets
- [ ] Implement circuit breaker pattern
- [ ] Write unit tests for service and strategy
- [ ] Integration test with real API
- [ ] Test Vietnamese language support
- [ ] Document API usage and cost tracking

---

## Success Criteria

- ✅ Successfully send messages to Gemini API
- ✅ Receive responses in Vietnamese
- ✅ Model selection works (70% Flash-Lite, 30% Pro)
- ✅ Retry logic handles 429 and 503 errors
- ✅ Circuit breaker opens after 5 consecutive failures
- ✅ Response time <1s for streaming
- ✅ Unit tests pass (100% coverage)
- ✅ Integration tests pass with real API
- ✅ API key stored securely (User Secrets)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| API key exposure | Low | Critical | Use User Secrets, Key Vault in prod |
| Rate limit exceeded | Medium | High | Implement retry with backoff, circuit breaker |
| High API costs | Medium | Medium | Hybrid model strategy, context summarization |
| Vietnamese language quality | Low | Medium | Test extensively, adjust prompts |
| API downtime | Low | High | Fallback responses, queue requests |

---

## Security Considerations

- **API Key Protection**: Never commit to source control, use User Secrets (dev) and Azure Key Vault (prod)
- **Input Validation**: Sanitize user input before sending to API
- **Output Filtering**: Validate responses before displaying to users
- **Rate Limiting**: Implement per-user rate limits to prevent abuse
- **Logging**: Don't log API keys or sensitive user data
- **Timeout**: Set reasonable timeout to prevent hanging requests

---

## Cost Estimation

### Assumptions
- 1000 conversations/day
- 10 turns per conversation
- 500 tokens per turn (input + output)
- 70% Flash-Lite, 30% Pro

### Calculation
- Daily tokens: 1000 × 10 × 500 = 5M tokens
- Flash-Lite: 5M × 0.7 = 3.5M tokens/day
- Pro: 5M × 0.3 = 1.5M tokens/day

### Monthly Cost
- Flash-Lite: 3.5M × 30 × $0.25/1M = $26.25/month
- Pro: 1.5M × 30 × $2.00/1M = $90/month
- **Total: ~$116/month** (vs $300/month with Pro-only)

### Cost per Conversation
- $116 / (1000 × 30) = **$0.0039 per conversation** (well under $0.10 target)

---

## Next Steps

After Phase 2 completion:
1. Proceed to Phase 3: State Machine
2. Integrate GeminiService into WebhookProcessor
3. Implement conversation history management
4. Build prompt templates for different scenarios
5. Set up cost monitoring and alerting
