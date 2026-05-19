# Phase 8: Testing

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Testing Strategies

## Overview
- **Priority:** P1 (High)
- **Status:** Pending
- **Mô tả:** Implement comprehensive unit và integration tests

## Key Insights
- Unit tests: signature validation, payload parsing, business logic
- Integration tests: full webhook flow với WebApplicationFactory
- E2E tests: real Facebook webhook deliveries (manual)
- Mock external dependencies (Facebook Graph API)

## Requirements

**Functional:**
- Unit tests cho tất cả services
- Integration tests cho endpoints
- Test coverage > 80%
- Test error scenarios
- Mock HttpClient cho Graph API

**Non-Functional:**
- Fast test execution (< 30s total)
- Isolated tests (no shared state)
- Clear test names
- Arrange-Act-Assert pattern

## Architecture

**Test Pyramid:**
```
E2E Tests (Manual)
    ↓
Integration Tests (WebApplicationFactory)
    ↓
Unit Tests (Moq, FluentAssertions)
```

## Related Code Files

**To Create:**
- `tests/MessengerWebhook.UnitTests/SignatureValidatorTests.cs`
- `tests/MessengerWebhook.UnitTests/WebhookProcessorTests.cs`
- `tests/MessengerWebhook.UnitTests/MessengerServiceTests.cs`
- `tests/MessengerWebhook.IntegrationTests/WebhookEndpointTests.cs`
- `tests/MessengerWebhook.IntegrationTests/TestWebApplicationFactory.cs`

## Implementation Steps

1. **SignatureValidatorTests**
```csharp
public class SignatureValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var appSecret = "test_secret";
        var rawBody = "{\"object\":\"page\"}";
        var validator = new SignatureValidator(
            Options.Create(new FacebookOptions { AppSecret = appSecret }),
            Mock.Of<ILogger<SignatureValidator>>());

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var signature = "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        // Act
        var result = await validator.ValidateAsync(rawBody, signature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_InvalidSignature_ReturnsFalse()
    {
        var validator = new SignatureValidator(...);
        var result = await validator.ValidateAsync("{}", "sha256=invalid");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_MissingPrefix_ReturnsFalse()
    {
        var validator = new SignatureValidator(...);
        var result = await validator.ValidateAsync("{}", "invalid_format");
        result.Should().BeFalse();
    }
}
```

2. **WebhookProcessorTests**
```csharp
public class WebhookProcessorTests
{
    [Fact]
    public async Task ProcessAsync_TextMessage_SendsEcho()
    {
        // Arrange
        var messengerService = new Mock<IMessengerService>();
        var processor = new WebhookProcessor(
            messengerService.Object,
            Mock.Of<ILogger<WebhookProcessor>>(),
            new MemoryCache(new MemoryCacheOptions()));

        var evt = new MessagingEvent(
            new Sender("user123"),
            new Recipient("page123"),
            1234567890,
            new Message("mid.123", "Hello", null),
            null);

        // Act
        await processor.ProcessAsync(evt);

        // Assert
        messengerService.Verify(
            x => x.SendTextMessageAsync("user123", It.Is<string>(s => s.Contains("Hello"))),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateMessage_Skips()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var messengerService = new Mock<IMessengerService>();
        var processor = new WebhookProcessor(messengerService.Object, Mock.Of<ILogger<WebhookProcessor>>(), cache);

        var evt = new MessagingEvent(..., new Message("mid.123", "Hello", null), null);

        // Act
        await processor.ProcessAsync(evt); // First time
        await processor.ProcessAsync(evt); // Duplicate

        // Assert
        messengerService.Verify(x => x.SendTextMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
```

3. **MessengerServiceTests**
```csharp
public class MessengerServiceTests
{
    [Fact]
    public async Task SendTextMessageAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"recipient_id\":\"123\",\"message_id\":\"mid.456\"}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new MessengerService(
            httpClient,
            Options.Create(new FacebookOptions { GraphApiBaseUrl = "https://graph.facebook.com", ApiVersion = "v21.0", PageAccessToken = "token" }),
            Mock.Of<ILogger<MessengerService>>());

        // Act
        var result = await service.SendTextMessageAsync("123", "Hello");

        // Assert
        result.RecipientId.Should().Be("123");
        result.MessageId.Should().Be("mid.456");
    }

    [Fact]
    public async Task SendTextMessageAsync_RateLimited_ThrowsException()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.TooManyRequests });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new MessengerService(httpClient, Options.Create(new FacebookOptions { ... }), Mock.Of<ILogger<MessengerService>>());

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.SendTextMessageAsync("123", "Hello"));
    }
}
```

4. **WebhookEndpointTests (Integration)**
```csharp
public class WebhookEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebhookEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWebhook_ValidParameters_ReturnsChallenge()
    {
        var client = _factory.CreateClient();
        var challenge = "test_challenge_123";

        var response = await client.GetAsync(
            $"/webhook?hub.mode=subscribe&hub.verify_token=test_token&hub.challenge={challenge}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be(challenge);
    }

    [Fact]
    public async Task PostWebhook_ValidPayload_Returns200()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "PAGE_ID",
                    time = 1234567890,
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "USER_ID" },
                            recipient = new { id = "PAGE_ID" },
                            timestamp = 1234567890,
                            message = new { mid = "MESSAGE_ID", text = "Hello" }
                        }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/webhook", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostWebhook_InvalidSignature_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook")
        {
            Content = new StringContent("{\"object\":\"page\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", "sha256=invalid");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

5. **TestWebApplicationFactory**
```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services với test doubles
            services.AddSingleton<IMessengerService, MockMessengerService>();
        });

        builder.UseEnvironment("Testing");
    }
}
```

6. **Run tests**
```bash
dotnet test --logger "console;verbosity=detailed"
dotnet test --collect:"XPlat Code Coverage"
```

## Todo List
- [ ] Write SignatureValidatorTests (3 tests)
- [ ] Write WebhookProcessorTests (2+ tests)
- [ ] Write MessengerServiceTests (2+ tests)
- [ ] Write WebhookEndpointTests (3+ tests)
- [ ] Create TestWebApplicationFactory
- [ ] Achieve > 80% code coverage
- [ ] All tests pass
- [ ] Document test execution trong README

## Success Criteria
- All unit tests pass
- All integration tests pass
- Code coverage > 80%
- No flaky tests
- Test execution < 30s
- Clear test failure messages

## Risk Assessment
- **Risk:** Flaky tests due to timing
  - **Mitigation:** Avoid Thread.Sleep, use proper async/await
- **Risk:** Tests coupled to implementation
  - **Mitigation:** Test behavior, not implementation details

## Security Considerations
- Don't commit real tokens trong test files
- Use test-specific configuration
- Mock external API calls

## Next Steps
- Phase 9: Deployment configuration
