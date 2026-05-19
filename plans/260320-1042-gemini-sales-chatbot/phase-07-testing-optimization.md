# Phase 7: Testing & Optimization

**Priority**: Critical
**Status**: Pending
**Duration**: 1.5 weeks
**Dependencies**: All previous phases (1-6)

---

## Context Links

- Research: [Gemini API Report](../reports/researcher-260320-1042-gemini-api.md)
- Research: [Order Management Report](../reports/researcher-260320-1042-order-management.md)
- All previous phases: [plan.md](./plan.md)

---

## Overview

Comprehensive testing, performance optimization, cost monitoring, security audit, and production hardening. Ensure system meets all success criteria before deployment.

---

## Key Insights

- Target: <1s response time, 99.5% availability, <$0.15 per conversation (Pro 50%)
- Load testing critical for 1000+ conversations/day + RAG queries
- Cost monitoring prevents budget overruns (Pro usage higher for cosmetics)
- Security audit required before production
- Fallback strategies ensure resilience
- RAG accuracy validation essential (>85% ingredient matching)

---

## Requirements

### Functional
- Unit tests for all services (100% coverage)
- Integration tests for full conversation flows
- Load testing for concurrent users
- Cost tracking and optimization
- Security vulnerability scanning
- Performance profiling and optimization
- Monitoring and alerting setup

### Non-Functional
- Response time <1s (streaming)
- RAG search <200ms (p95)
- Availability 99.5%
- Cost <$0.15 per conversation (Pro 50%)
- Support 1000+ conversations/day
- Database queries <50ms
- Skin profile extraction accuracy >90%
- Ingredient match relevance >85%
- Zero critical security vulnerabilities

---

## Architecture

### Testing Strategy
```
Unit Tests → Integration Tests → Load Tests → Security Audit → Production Deploy
```

### Monitoring Stack
```
Application Insights / Prometheus
  ├── API latency metrics
  ├── Gemini API costs
  ├── Error rates
  ├── Database performance
  └── User engagement metrics
```

---

## Related Code Files

### To Create
- `tests/MessengerWebhook.Tests/Services/GeminiServiceTests.cs`
- `tests/MessengerWebhook.Tests/Services/ProductServiceTests.cs`
- `tests/MessengerWebhook.Tests/Services/CartServiceTests.cs`
- `tests/MessengerWebhook.Tests/Services/OrderServiceTests.cs`
- `tests/MessengerWebhook.Tests/StateMachine/StateMachineTests.cs`
- `tests/MessengerWebhook.Tests/Integration/ConversationFlowTests.cs`
- `tests/MessengerWebhook.Tests/Integration/OrderFlowTests.cs`
- `tests/MessengerWebhook.Tests/Load/LoadTests.cs`
- `src/MessengerWebhook/Services/Monitoring/ICostTracker.cs`
- `src/MessengerWebhook/Services/Monitoring/CostTracker.cs`
- `src/MessengerWebhook/Services/Monitoring/MetricsCollector.cs`
- `src/MessengerWebhook/Middleware/PerformanceMonitoringMiddleware.cs`
- `scripts/load-test.sh`
- `scripts/security-scan.sh`

### To Modify
- `src/MessengerWebhook/Program.cs` (add monitoring)
- `src/MessengerWebhook/appsettings.json` (monitoring config)

---

## Implementation Steps

### 1. Set Up Test Project
```bash
cd "D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook"
dotnet new xunit -n MessengerWebhook.Tests -o tests/MessengerWebhook.Tests
cd tests/MessengerWebhook.Tests
dotnet add reference ../../src/MessengerWebhook/MessengerWebhook.csproj
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

### 2. Write Unit Tests for GeminiService
```csharp
public class GeminiServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly Mock<IModelSelectionStrategy> _mockStrategy;
    private readonly GeminiService _service;

    [Fact]
    public async Task SendMessageAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        var mockResponse = new GeminiResponse
        {
            Candidates = new[]
            {
                new Candidate
                {
                    Content = new Content
                    {
                        Parts = new[] { new Part { Text = "Test response" } }
                    }
                }
            }
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mockResponse))
            });

        // Act
        var result = await _service.SendMessageAsync("user123", "Hello", new List<ConversationMessage>());

        // Assert
        result.Should().Be("Test response");
    }

    [Fact]
    public async Task SendMessageAsync_RateLimitError_RetriesWithBackoff()
    {
        // Arrange
        var callCount = 0;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(mockResponse))
                };
            });

        // Act
        var result = await _service.SendMessageAsync("user123", "Hello", new List<ConversationMessage>());

        // Assert
        callCount.Should().Be(3);
        result.Should().NotBeEmpty();
    }
}
```

### 3. Write Integration Tests
```csharp
public class ConversationFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    [Fact]
    public async Task FullConversationFlow_BrowseToOrder_Succeeds()
    {
        // Arrange
        var psid = "test_user_123";

        // Act & Assert - Greeting
        var greeting = await SendMessage(psid, "Xin chào");
        greeting.Should().Contain("chào mừng");

        // Browse products
        var browse = await SendMessage(psid, "Tôi muốn xem áo sơ mi");
        browse.Should().NotBeEmpty();

        // Select product (via postback)
        await SendPostback(psid, "VIEW_PRODUCT_1");

        // Select size
        await SendPostback(psid, "SELECT_SIZE_1_M");

        // Select color
        await SendPostback(psid, "SELECT_COLOR_1_White");

        // Add to cart
        await SendPostback(psid, "ADD_TO_CART");

        // Checkout
        await SendPostback(psid, "CHECKOUT");

        // Provide address
        var address = await SendMessage(psid, "Nguyen Van A\n123 Le Loi\nHCM, Vietnam");
        address.Should().Contain("số điện thoại");

        // Provide phone
        var phone = await SendMessage(psid, "0901234567");
        phone.Should().Contain("phương thức thanh toán");

        // Select payment
        await SendPostback(psid, "PAYMENT_COD");

        // Verify order created
        var order = await GetLastOrder(psid);
        order.Should().NotBeNull();
        order.Status.Should().Be("draft");
    }

    private async Task<string> SendMessage(string psid, string text)
    {
        var webhook = new WebhookEvent
        {
            Object = "page",
            Entry = new[]
            {
                new Entry
                {
                    Messaging = new[]
                    {
                        new MessagingEvent
                        {
                            Sender = new Sender { Id = psid },
                            Message = new Message { Text = text }
                        }
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/webhook", webhook);
        response.EnsureSuccessStatusCode();

        // Get response from mock messenger service
        return await GetLastResponse(psid);
    }
}
```

### 4. Implement Cost Tracker
```csharp
public interface ICostTracker
{
    Task TrackApiCallAsync(string model, int inputTokens, int outputTokens);
    Task<decimal> GetDailyCostAsync(DateTime date);
    Task<decimal> GetMonthlyCostAsync(int year, int month);
    Task<CostReport> GetCostReportAsync(DateTime startDate, DateTime endDate);
}

public class CostTracker : ICostTracker
{
    private readonly ILogger<CostTracker> _logger;
    private readonly Dictionary<string, decimal> _pricing = new()
    {
        ["gemini-3.1-pro"] = 2.00m / 1_000_000,
        ["gemini-3.1-flash-lite"] = 0.25m / 1_000_000
    };

    public async Task TrackApiCallAsync(string model, int inputTokens, int outputTokens)
    {
        var totalTokens = inputTokens + outputTokens;
        var cost = totalTokens * _pricing[model];

        _logger.LogInformation(
            "Gemini API call: Model={Model}, Tokens={Tokens}, Cost=${Cost:F4}",
            model, totalTokens, cost);

        // Store in database or metrics system
        await StoreMetricAsync(new ApiCallMetric
        {
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Cost = cost,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task<CostReport> GetCostReportAsync(DateTime startDate, DateTime endDate)
    {
        var metrics = await GetMetricsAsync(startDate, endDate);

        return new CostReport
        {
            TotalCost = metrics.Sum(m => m.Cost),
            TotalCalls = metrics.Count,
            AverageCostPerCall = metrics.Average(m => m.Cost),
            ModelBreakdown = metrics.GroupBy(m => m.Model)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.Cost))
        };
    }
}
```

### 5. Implement Performance Monitoring Middleware
```csharp
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            if (sw.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning(
                    "Slow request: {Method} {Path} took {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    sw.ElapsedMilliseconds);
            }

            // Track metrics
            context.Items["RequestDuration"] = sw.ElapsedMilliseconds;
        }
    }
}
```

### 6. Set Up Load Testing
```bash
# scripts/load-test.sh
#!/bin/bash

# Install k6 if not present
if ! command -v k6 &> /dev/null; then
    echo "Installing k6..."
    # Windows: choco install k6
    # Linux: sudo apt-get install k6
fi

# Run load test
k6 run --vus 100 --duration 5m load-test.js
```

```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '1m', target: 50 },  // Ramp up to 50 users
        { duration: '3m', target: 100 }, // Stay at 100 users
        { duration: '1m', target: 0 },   // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<1000'], // 95% of requests under 1s
        http_req_failed: ['rate<0.01'],    // Less than 1% errors
    },
};

export default function () {
    const payload = JSON.stringify({
        object: 'page',
        entry: [{
            messaging: [{
                sender: { id: `user_${__VU}` },
                message: { text: 'Xin chào' }
            }]
        }]
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'X-Hub-Signature-256': 'sha256=test' // Mock signature
        },
    };

    let res = http.post('http://localhost:5000/webhook', payload, params);

    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time < 1s': (r) => r.timings.duration < 1000,
    });

    sleep(1);
}
```

### 7. Security Audit Checklist
```markdown
## Security Audit Checklist

### API Security
- [ ] API keys stored in Key Vault (not appsettings.json)
- [ ] HTTPS enforced for all endpoints
- [ ] Webhook signature validation enabled
- [ ] Rate limiting implemented per user
- [ ] Input validation on all endpoints

### Data Security
- [ ] No PII in logs
- [ ] Database connection encrypted
- [ ] Passwords hashed (if applicable)
- [ ] No credit card storage (PCI compliance)
- [ ] SQL injection prevention (parameterized queries)

### Authentication & Authorization
- [ ] Webhook verification token validated
- [ ] HMAC signature verification working
- [ ] No hardcoded credentials in code

### Dependencies
- [ ] All NuGet packages up to date
- [ ] No known vulnerabilities (dotnet list package --vulnerable)
- [ ] Dependency scanning in CI/CD

### Monitoring
- [ ] Error logging enabled
- [ ] Security events logged
- [ ] Alerting configured for anomalies
```

### 8. Performance Optimization
```csharp
// Optimize database queries with compiled queries
public class OptimizedProductRepository
{
    private static readonly Func<MessengerBotDbContext, string, IAsyncEnumerable<Product>>
        GetByCategoryQuery = EF.CompileAsyncQuery(
            (MessengerBotDbContext context, string category) =>
                context.Products
                    .Where(p => p.Category == category && p.IsActive)
                    .Include(p => p.Variants)
                    .OrderBy(p => p.Name));

    public async Task<List<Product>> GetByCategoryAsync(string category)
    {
        return await GetByCategoryQuery(_context, category).ToListAsync();
    }
}

// Add response caching
public class CachedProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly IMemoryCache _cache;

    public async Task<List<ProductDto>> GetByCategoryAsync(string category, int page = 1, int pageSize = 10)
    {
        var cacheKey = $"products:{category}:{page}:{pageSize}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _inner.GetByCategoryAsync(category, page, pageSize);
        });
    }
}
```

### 9. Monitoring Dashboard Setup
```csharp
// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// Custom metrics
public class MetricsCollector
{
    private readonly TelemetryClient _telemetry;

    public void TrackConversation(string psid, int turns, decimal cost)
    {
        _telemetry.TrackEvent("ConversationCompleted", new Dictionary<string, string>
        {
            ["PSID"] = psid,
            ["Turns"] = turns.ToString(),
            ["Cost"] = cost.ToString("F4")
        });
    }

    public void TrackOrderCreated(int orderId, decimal amount)
    {
        _telemetry.TrackEvent("OrderCreated", new Dictionary<string, string>
        {
            ["OrderId"] = orderId.ToString(),
            ["Amount"] = amount.ToString("N0")
        });
    }
}
```

### 10. Alerting Configuration
```json
// appsettings.json
{
  "Monitoring": {
    "Alerts": {
      "ErrorRateThreshold": 0.05,
      "LatencyThresholdMs": 2000,
      "DailyCostThreshold": 10.0,
      "AlertEmail": "admin@example.com"
    }
  }
}
```

### 11. Run All Tests
```bash
# Unit tests
dotnet test tests/MessengerWebhook.Tests/MessengerWebhook.Tests.csproj

# Integration tests
dotnet test tests/MessengerWebhook.Tests/MessengerWebhook.Tests.csproj --filter Category=Integration

# Load tests
./scripts/load-test.sh

# Security scan
dotnet list package --vulnerable
```

### 12. Performance Benchmarking
```csharp
[MemoryDiagnoser]
public class PerformanceBenchmarks
{
    private IProductService _productService;

    [Benchmark]
    public async Task GetProductsByCategory()
    {
        await _productService.GetByCategoryAsync("shirts");
    }

    [Benchmark]
    public async Task SearchProducts()
    {
        await _productService.SearchAsync("áo sơ mi");
    }
}
```

---

## Todo List

- [ ] Set up test project with xUnit, Moq, FluentAssertions
- [ ] Write unit tests for all services (100% coverage)
- [ ] Write integration tests for conversation flows
- [ ] Write integration tests for order flow
- [ ] Implement cost tracking service
- [ ] Implement performance monitoring middleware
- [ ] Set up load testing with k6
- [ ] Run security audit checklist
- [ ] Optimize database queries (compiled queries)
- [ ] Add response caching
- [ ] Set up Application Insights
- [ ] Configure alerting
- [ ] Run all tests and verify passing
- [ ] Performance benchmarking
- [ ] Document test results

---

## Success Criteria

- Unit test coverage >95%
- All integration tests pass
- Load test: 100 concurrent users, <1s response time
- Cost: <$0.10 per conversation (verified)
- No critical security vulnerabilities
- Database queries <50ms (p95)
- Error rate <1%
- Availability 99.5%
- Monitoring and alerting operational

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Performance degradation under load | Medium | High | Load testing, optimization, caching |
| Cost overruns | Medium | Medium | Cost tracking, spend caps, alerts |
| Security vulnerabilities | Low | Critical | Security audit, dependency scanning |
| Test coverage gaps | Low | Medium | Code coverage reports, review |

---

## Security Considerations

- Run OWASP dependency check
- Scan for SQL injection vulnerabilities
- Verify HTTPS enforcement
- Test rate limiting effectiveness
- Audit logging for sensitive operations
- Penetration testing (optional)

---

## Performance Targets

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Response time (p95) | <1s | TBD | Pending |
| Database queries (p95) | <50ms | TBD | Pending |
| Error rate | <1% | TBD | Pending |
| Availability | 99.5% | TBD | Pending |
| Cost per conversation | <$0.10 | TBD | Pending |
| Concurrent users | 100+ | TBD | Pending |

---

## Cost Analysis

### Actual vs Projected
```
Projected: $116/month (1000 conversations/day)
Actual: TBD after testing

Breakdown:
- Gemini Flash-Lite: 70% of calls
- Gemini Pro: 30% of calls
- Database: $X/month
- Hosting: $X/month
```

---

## Next Steps

After Phase 7 completion:
1. Deploy to staging environment
2. Run final acceptance tests
3. Prepare production deployment
4. Set up monitoring dashboards
5. Train support team
6. Plan gradual rollout (10% → 50% → 100%)
