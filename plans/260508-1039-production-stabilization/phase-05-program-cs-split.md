# Phase 05: Program.cs Modularization

**Priority**: P2 — quick win, parallel-safe
**Effort**: 1 ngày
**Status**: Complete (2026-05-12)
**Depends on**: None (có thể làm song song bất kỳ phase nào)

## Context

`Program.cs` hiện tại 643-725 dòng, 119 DI registrations + middleware + auth + endpoint mapping + Serilog config + Channel config + migration trigger.

Hệ quả với team 2:
- Mỗi feature mới chạm Program.cs → merge conflict cao
- Khó review (PR diff lớn)
- Khó test integration (composition root monolithic)

## Mục tiêu

Tách Program.cs thành extension methods theo module. Target: **Program.cs < 100 dòng**.

## Files to modify

- `Program.cs` — gọi extension methods

## Files to create

- `Configuration/ServiceRegistration/AiServicesRegistration.cs`
- `Configuration/ServiceRegistration/CacheServicesRegistration.cs`
- `Configuration/ServiceRegistration/SalesPipelineRegistration.cs`
- `Configuration/ServiceRegistration/AdminModuleRegistration.cs`
- `Configuration/ServiceRegistration/MessengerServicesRegistration.cs`
- `Configuration/ServiceRegistration/PersistenceRegistration.cs`
- `Configuration/ServiceRegistration/ObservabilityRegistration.cs`
- `Configuration/ServiceRegistration/BackgroundServicesRegistration.cs`

## Module breakdown

### `AddPersistence(this IServiceCollection services, IConfiguration config)`
- DbContext + DbContextFactory
- Migration trigger
- Repository registrations

### `AddAiServices(this IServiceCollection services, IConfiguration config)`
- GeminiService, GeminiEmbeddingService
- AI handlers, strategies
- Vertex AI client

### `AddCacheServices(this IServiceCollection services, IConfiguration config)`
- Redis distributed cache
- CacheKeyGenerator, CacheInvalidationService
- Decorator: EmbeddingCacheService, ResultCacheService

### `AddSalesPipeline(this IServiceCollection services)`
- Naturalness pipeline: Emotion, Tone, ContextAnalyzer, SmallTalk, ResponseValidation
- SubIntent classifiers
- ProductGrounding services
- DraftOrderCoordinator
- All 17 state handlers

### `AddMessengerServices(this IServiceCollection services, IConfiguration config)`
- MessengerService, WebhookProcessor
- QuickReplyHandler
- LiveCommentAutomationService
- HttpClient setup

### `AddAdminModule(this IServiceCollection services, IConfiguration config)`
- AdminAuthService, AdminDashboardQueryService
- Identity setup
- Admin endpoints

### `AddObservability(this IServiceCollection services, IConfiguration config)`
- OpenTelemetry (từ Phase 01)
- Serilog
- BotMetrics (từ Phase 02)
- Health checks

### `AddBotBackgroundServices(this IServiceCollection services)`
- All hosted services
- Channel config

## Implementation steps

### Step 1: Tạo skeleton extension classes (1h)
8 file `*Registration.cs` rỗng với method signature đúng.

### Step 2: Migrate từng module (5h)
**Thứ tự**: Persistence → Observability → Cache → AI → Messenger → SalesPipeline → Admin → BackgroundServices

Mỗi module:
1. Cut các dòng tương ứng từ Program.cs
2. Paste vào extension method
3. Add using nếu cần
4. Build → fix compile error
5. Run integration test → confirm DI resolve

### Step 3: Refactor Program.cs (1h)

Mục tiêu structure:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDotNetEnv();

builder.Services
    .AddPersistence(builder.Configuration)
    .AddObservability(builder.Configuration)
    .AddCacheServices(builder.Configuration)
    .AddAiServices(builder.Configuration)
    .AddMessengerServices(builder.Configuration)
    .AddSalesPipeline()
    .AddAdminModule(builder.Configuration)
    .AddBotBackgroundServices();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SignatureValidationMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();
app.MapAdminEndpoints();
app.MapInternalOperationsEndpoints();

app.Run();
```

### Step 4: Validation (1h)
- `dotnet build` clean
- `dotnet test` toàn bộ pass
- Run app local, send test webhook → verify response
- Diff so với master: không có behavior change, chỉ cấu trúc

## Acceptance criteria

- [ ] Program.cs ≤ 100 dòng
- [ ] 0 test fail
- [ ] App start thành công, webhook xử lý đúng
- [ ] Mỗi extension class < 150 dòng (nếu lớn hơn → tách tiếp)
- [ ] DI graph identical (verify qua `IServiceProvider.GetService<T>()` cho 10 service mẫu)

## Rollback

Pure refactor, không thay đổi behavior. Revert commit nếu fail.

## Risk

| Risk | Mitigation |
|------|------------|
| Order DI registration matter cho Decorator pattern | Verify `Decorate<>` được gọi sau khi service gốc registered |
| Thiếu service registration sau migrate | Integration test phải resolve mọi controller + handler |
| Conflict với feature đang làm | Làm trên branch riêng, merge cuối tuần khi ít hoạt động |

## Unresolved questions

1. Có nên giữ `Program.cs` hay đổi sang `Bootstrap.cs` + minimal `Program.cs`? — đề xuất giữ tên Program.cs (.NET convention)
2. Extension class đặt trong namespace nào? `MessengerWebhook.Configuration.ServiceRegistration` hay flat `MessengerWebhook.Configuration`?
