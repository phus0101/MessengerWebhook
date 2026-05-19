# Phase 02: LLM Provider Resilience

**Priority**: P0  
**Effort**: 2-3 ngày  
**Status**: Complete  
**Depends on**: Phase 01 (DI fix)

---

## Vấn đề

Hệ thống 100% phụ thuộc Gemini. Không có circuit breaker, không có graceful degradation. Khi Gemini down hoặc rate limit → toàn bộ chatbot trả lỗi hoặc timeout.

`GeminiRetryHandler` chỉ retry — không có circuit state, không fallback provider/response.

Research doc: *"provider-agnostic: có lớp router, fallback, schema chuẩn"* — điều này áp dụng ngay cả khi chỉ dùng 1 provider (cần circuit breaker + degraded mode).

---

## Mục tiêu

1. **Circuit breaker** — Gemini tự động ngắt sau N failure liên tiếp, không xếp hàng requests vô hạn
2. **Graceful degradation** — Pre-canned responses khi LLM unavailable (không để khách nhận timeout trống)
3. **Provider-agnostic interface** — Đổi tên/mở rộng interface để sẵn sàng thêm provider thứ 2 sau này
4. **Observability** — Log circuit state transitions, fallback counts

---

## Thiết kế

### Không thêm provider thứ 2 trong phase này

Lý do: overhead thêm API key, service implementation, test suite cho 2-person team. Scope = interface abstraction + resilience, không thêm Anthropic/OpenAI.

### Circuit Breaker với Polly v8

```csharp
// Pattern: Polly CircuitBreaker wrapping GeminiService HTTP calls
services.AddHttpClient<IGeminiService, GeminiService>()
    .AddResilienceHandler("gemini-pipeline", builder =>
    {
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,          // 50% failure rate
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            OnOpened = args => { /* log */ },
            OnClosed = args => { /* log */ }
        });
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential
        });
        builder.AddTimeout(TimeSpan.FromSeconds(15));
    });
```

### Graceful Degradation Service

```csharp
public interface ILlmFallbackService
{
    bool IsLlmAvailable { get; }
    string GetDegradedResponse(ConversationState state, CustomerIntent? intent);
}
```

Pre-canned responses theo state:
- `Consulting`: "Dạ em đang bận một chút, chị nhắn lại sau vài phút nha ạ."
- `CollectingInfo`: "Dạ em ghi nhận thông tin rồi ạ, chị chờ em xử lý một chút."
- Mặc định: `SalesBotOptions.UnsupportedFallbackMessage`

### Interface abstraction (không đổi tên, thêm provider-agnostic wrapper)

Giữ `IGeminiService` nguyên (breaking change không đáng). Thêm `ILlmOrchestrator` wrapper nhẹ:

```csharp
public interface ILlmOrchestrator
{
    Task<string> GenerateAsync(LlmRequest request, CancellationToken ct = default);
    bool IsAvailable { get; }
}

// Implementation delegates sang IGeminiService với circuit breaker state check
public class GeminiLlmOrchestrator : ILlmOrchestrator
{
    private readonly IGeminiService _gemini;
    private readonly ILlmFallbackService _fallback;
    
    public bool IsAvailable => !_circuitOpen;
    
    public async Task<string> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        if (!IsAvailable)
            return _fallback.GetDegradedResponse(request.State, request.Intent);
        
        try { return await _gemini.SendMessageAsync(...); }
        catch (BrokenCircuitException) { return _fallback.GetDegradedResponse(...); }
    }
}
```

**Chú ý**: `SalesReplyOrchestrator` và `SalesStateHandlerBase` tiếp tục dùng `IGeminiService` trực tiếp. `ILlmOrchestrator` là wrapper mới cho các call mới, migration dần.

---

## Files cần tạo

- `Services/AI/Resilience/ILlmFallbackService.cs`
- `Services/AI/Resilience/LlmFallbackService.cs`
- `Services/AI/Resilience/LlmDegradedResponses.cs` — const strings theo state

## Files cần sửa

- `Configuration/ServiceRegistration/AiServicesRegistration.cs` — thay `.AddHttpMessageHandler<GeminiRetryHandler>()` bằng Polly resilience pipeline đầy đủ
- `Services/AI/Handlers/GeminiRetryHandler.cs` — có thể xóa nếu Polly retry thay thế hoàn toàn
- `Configuration/ServiceRegistration/SalesPipelineRegistration.cs` — đăng ký `ILlmFallbackService`

---

## Implementation Steps

### Step 1: Audit GeminiRetryHandler (0.25 ngày)

```csharp
// Đọc GeminiRetryHandler để xác định:
// - Retry policy hiện tại là gì?
// - Có conflict với Polly retry mới không?
// Nếu Polly thay thế hoàn toàn → xóa GeminiRetryHandler
```

### Step 2: Cài Polly resilience pipeline (0.5 ngày)

Sửa `AiServicesRegistration.cs`:
```csharp
services.AddHttpClient<IGeminiService, GeminiService>()
    .ConfigureHttpClient(...)
    .AddHttpMessageHandler<GeminiAuthHandler>()
    // Xóa GeminiRetryHandler nếu Polly thay thế
    .AddResilienceHandler("gemini", ConfigureGeminiResilience)
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

static void ConfigureGeminiResilience(ResiliencePipelineBuilder<HttpResponseMessage> builder)
{
    builder.AddCircuitBreaker(new() { ... });
    builder.AddRetry(new() { MaxRetryAttempts = 2, ... });
    builder.AddTimeout(TimeSpan.FromSeconds(15));
}
```

### Step 3: Tạo LlmFallbackService (0.5 ngày)

`LlmFallbackService` đọc pre-canned responses từ `SalesBotOptions` hoặc hardcoded constants. Không gọi LLM.

### Step 4: Tích hợp vào SalesStateHandlerBase (0.5 ngày)

```csharp
// SalesStateHandlerBase.HandleAsync — catch BrokenCircuitException
catch (BrokenCircuitException)
{
    var degraded = _fallback.GetDegradedResponse(ctx.CurrentState, null);
    ConversationHistoryHelper.AddToHistory(ctx, "assistant", degraded, ...);
    return degraded;
}
```

### Step 5: Tests + Observability (0.5 ngày)

- Unit test: mock circuit open → verify fallback response returned
- Log `[LlmCircuit] State=Open/Closed` với Serilog
- Metric: increment `llm.circuit_break.count` counter

---

## Todo

- [ ] Đọc GeminiRetryHandler, quyết định xóa hay giữ
- [ ] Cài Polly circuit breaker + retry + timeout pipeline
- [ ] Tạo ILlmFallbackService + LlmFallbackService
- [ ] Tạo LlmDegradedResponses constants
- [ ] Đăng ký trong AiServicesRegistration
- [ ] Bắt BrokenCircuitException trong SalesStateHandlerBase
- [ ] Unit test circuit breaker fallback
- [ ] Log state transitions
- [ ] Build + tests pass

---

## Success Criteria

- `dotnet test` pass
- Khi mock Gemini trả lỗi liên tiếp → circuit opens → fallback response được trả
- Không còn timeout trống khi Gemini down
- Log rõ khi circuit open/close

---

## Risk

- **Polly version conflict**: `Microsoft.Extensions.Http.Resilience` 10.4.0 đã có trong csproj — compatible với .AddResilienceHandler()
- **GeminiRetryHandler + Polly retry double-retry**: Cần xóa GeminiRetryHandler nếu thêm Polly retry để tránh nhân đôi
- **Circuit breaker quá nhạy**: FailureRatio 0.5 với SamplingDuration 30s — tune sau khi có baseline metrics từ Phase 02 (production stabilization)
