# Phase 01: Structured Logging + Seq + Correlation ID

**Priority**: P0 — Tiên quyết
**Effort**: 2 ngày
**Status**: COMPLETED (2026-05-13)
**Backend chosen**: Seq (self-host, free 1-user, hoặc Docker)

## Context

- 1000 tenant production, Serilog file log có sẵn nhưng **không correlate được** giữa các step trong cùng 1 conversation
- Pipeline phức tạp: Webhook → Channel → StateHandler → Naturalness Pipeline → Cache → Pinecone → Gemini
- Khi tenant complain "bot lag" → grep file log mất hàng giờ, vẫn khó link request
- Đã chọn **Seq** (over Honeycomb) vì: chi phí $0, query SQL-like, tích hợp trực tiếp Serilog đã dùng

## Mục tiêu

1. Mọi webhook request có **correlation ID duy nhất**, propagate qua mọi log entry
2. Structured logging: log với property thay vì string, queryable trong Seq
3. Tag mọi log với: `tenant_id`, `psid_hash`, `correlation_id`, `state`, `sub_intent`
4. Push log realtime sang **Seq** (self-host Docker)
5. Tạo dashboard cơ bản trong Seq cho 5 query phổ biến nhất
6. Optional (nếu time): OTLP trace export sang Seq cho waterfall view

## Files to modify

- `Program.cs` — Serilog config: thêm Seq sink, structured properties
- `Middleware/SignatureValidationMiddleware.cs` — wrap với correlation context
- `Middleware/TenantResolutionMiddleware.cs` — add tenant_id vào LogContext
- `Services/WebhookProcessor.cs` — log entry/exit với correlation
- `Services/AI/GeminiService.cs` — log latency mỗi LLM call
- `Services/VectorSearch/PineconeVectorService.cs` — log search latency
- `Services/Cache/EmbeddingCacheService.cs`, `ResultCacheService.cs` — log cache hit/miss
- `StateMachine/Handlers/SalesStateHandlerBase.cs` — log mỗi pipeline step

## Files to create

- `Configuration/SeqOptions.cs` — config Seq endpoint, API key
- `Middleware/CorrelationIdMiddleware.cs` — sinh + propagate correlation ID
- `Services/Observability/LogContextEnricher.cs` — Serilog enricher cho tenant_id, psid_hash
- `Services/Observability/PiiRedactor.cs` — hash PSID, mask phone (sẽ reuse trong Phase 03 H1)
- `docker-compose.observability.yml` — Seq service definition

## Implementation steps

### Step 1: Setup Seq self-host (0.25 ngày)

**Docker Compose** thêm vào project (hoặc `docker-compose.observability.yml` riêng):

```yaml
services:
  seq:
    image: datalust/seq:latest
    container_name: seq
    environment:
      - ACCEPT_EULA=Y
      - SEQ_FIRSTRUN_ADMINPASSWORDHASH=<bcrypt-hash>
    ports:
      - "5341:5341"   # ingestion
      - "8080:80"     # UI
    volumes:
      - seq-data:/data
    restart: unless-stopped

volumes:
  seq-data:
```

**Production**: deploy Seq trên VPS riêng (1GB RAM đủ cho 1000 tenant), hoặc dùng Seq Cloud nếu muốn ($60/month bắt đầu).

### Step 2: Serilog Seq sink (0.25 ngày)

**Add package**:
```xml
<PackageReference Include="Serilog.Sinks.Seq" Version="8.0.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />
<PackageReference Include="Serilog.Enrichers.Thread" Version="4.0.0" />
```

**Update Program.cs**:
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "MessengerWebhook")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Seq:ServerUrl"]!,
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();
```

**appsettings.json**:
```json
{
  "Seq": {
    "ServerUrl": "http://seq:5341",
    "ApiKey": "{{SEQ_INGESTION_API_KEY}}"
  }
}
```

### Step 3: CorrelationIdMiddleware (0.25 ngày)

```csharp
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        context.Items["CorrelationId"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
```

Đăng ký **đầu tiên** trong pipeline middleware.

### Step 4: Enricher cho tenant_id + psid_hash (0.25 ngày)

```csharp
// LogContextEnricher.cs — gọi sau TenantResolutionMiddleware
public static class LogContextHelpers
{
    public static IDisposable PushTenantContext(string tenantId, string? psid = null)
    {
        var disposables = new List<IDisposable>
        {
            LogContext.PushProperty("TenantId", tenantId)
        };
        if (psid != null)
        {
            disposables.Add(LogContext.PushProperty("PsidHash", PiiRedactor.HashPsid(psid)));
        }
        return new CompositeDisposable(disposables);
    }
}
```

Update `WebhookProcessor.cs`:
```csharp
using var _ = LogContextHelpers.PushTenantContext(tenantId, psid);
Logger.LogInformation("Webhook received {EventType}", eventType);
```

### Step 5: Structured log cho hot paths (0.5 ngày)

Replace string interpolation bằng structured properties:

**TRƯỚC**:
```csharp
Logger.LogInformation($"Gemini call took {elapsed.TotalMilliseconds}ms for tenant {tenantId}");
```

**SAU**:
```csharp
Logger.LogInformation(
    "Gemini call completed in {ElapsedMs}ms for prompt of {PromptLength} chars (cache: {CacheHit})",
    elapsed.TotalMilliseconds, promptLength, cacheHit);
```

Hot paths cần structured log:
- `WebhookProcessor`: webhook received, parsed, queued, processed
- `GeminiService.GenerateAsync`: call start, call end với ElapsedMs, TokenCount
- `PineconeVectorService.SearchAsync`: search start, search end với ElapsedMs, ResultCount
- `EmbeddingCacheService.GetEmbeddingAsync`: cache hit/miss
- `SalesStateHandlerBase.HandleAsync`: pipeline step entry với StepName
- Mọi exception: log với full context

### Step 6: Seq dashboard (0.25 ngày)

Tạo 5 saved query trong Seq UI:

**Query 1: Slow webhook (>5s)**
```
@Application = 'MessengerWebhook' 
and ElapsedMs > 5000
| order by @Timestamp desc
| limit 100
```

**Query 2: Conversation theo PSID hash**
```
PsidHash = 'abc123...' 
and @Timestamp > now() - 1h
| order by @Timestamp asc
```

**Query 3: Error rate per tenant**
```
@Level in ['Error', 'Fatal']
| summarize ErrorCount = count() by TenantId
| order by ErrorCount desc
```

**Query 4: Gemini latency p95**
```
@MessageTemplate startswith 'Gemini call completed'
| summarize p95 = percentile(ElapsedMs, 95) by bin(@Timestamp, 5m)
```

**Query 5: Cache hit rate**
```
@MessageTemplate startswith 'Cache lookup'
| summarize HitRate = countif(CacheHit = true) * 100.0 / count() by bin(@Timestamp, 5m)
```

### Step 7 (Optional): Add OTLP trace export (0.25 ngày)

Nếu time cho phép — bonus tính năng waterfall trace trong Seq:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o => {
            o.Endpoint = new Uri(builder.Configuration["Seq:OtlpEndpoint"]!);
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.Headers = $"X-Seq-ApiKey={builder.Configuration["Seq:ApiKey"]}";
        }));
```

Seq 2024.1+ ingest OTLP traces native — không cần collector trung gian.

## Acceptance criteria

- [ ] Seq deployed (Docker), accessible UI
- [ ] Mọi log entry có `CorrelationId`, `TenantId`, `PsidHash` properties
- [ ] 1 webhook request → 1 correlation ID → search Seq thấy mọi step liên quan
- [ ] 5 saved query hoạt động với data thật
- [ ] Log overhead < 5ms p95 (Seq sink async)
- [ ] PII (raw phone, raw PSID, address) KHÔNG có trong log (verify regex scan)
- [ ] File log local vẫn ghi (backup), Seq là primary query interface

## Rollback plan

- Seq sink fail → Serilog tự động fallback console + file (resilient by default)
- Feature flag `Seq:Enabled` trong appsettings, disable sink khi cần
- Không thay đổi DB, không migration — revert config đủ

## Risk

| Risk | Mitigation |
|------|------------|
| Seq down → block app | Serilog buffered async sink, không block request thread |
| Disk Seq đầy (default unlimited retention) | Set retention 14-30 ngày qua Seq settings |
| Log volume vượt expectation | Sampling cho INFO level, giữ 100% Warning+ |
| PSID raw vào log = PII leak | Compile-time check qua Roslyn analyzer (optional), code review |
| API key Seq leak | Lưu trong `.env`, không commit; rotate quarterly |
| 1 tenant high-volume nuốt log | Per-tenant rate limit log entries (Serilog filter) |

## Cost estimate

- **Seq self-host VPS**: ~$10/tháng (1GB RAM, 50GB disk DigitalOcean/Vultr)
- **Seq Cloud (alternative)**: $60/tháng entry tier
- **Seq License**: Free 1 user, $456/year unlimited users (chỉ cần khi team > 1)

## Decisions chốt

- ✅ **Deployment**: cùng VPS với app (Docker Compose chung)
- ✅ **Retention**: 14 ngày
- ✅ **Multi-environment**: 1 Seq instance, filter qua `Environment` property

## Cùng VPS — risk mitigation cụ thể

Vì cùng VPS, Seq crash có thể kéo theo app. Mitigation:

1. **Resource limit Seq container**:
   ```yaml
   seq:
     image: datalust/seq:latest
     deploy:
       resources:
         limits:
           memory: 1G
           cpus: '0.5'
   ```
   → Seq không thể nuốt RAM/CPU của app

2. **Healthcheck**:
   ```yaml
   seq:
     healthcheck:
       test: ["CMD", "curl", "-f", "http://localhost/health"]
       interval: 30s
       timeout: 5s
       retries: 3
   ```

3. **Serilog Seq sink async + buffered**:
   - `Seq(serverUrl, batchPostingLimit: 100, period: TimeSpan.FromSeconds(2))`
   - Seq down → log buffer trong app, fallback file log
   - App không block khi Seq lag

4. **Disk space alert**: Seq retention 14 ngày + 1000 tenant volume cần ước lượng ~5-10GB. Set alert khi disk > 80%.

5. **Backup**: cron daily `docker exec seq seq-backup` xuất sang external storage (S3-compatible / Wasabi / Backblaze ~$0.5/tháng)

## Unresolved questions

1. ~~Deployment location~~ ✅ same VPS
2. ~~Retention~~ ✅ 14 ngày
3. **Có Roslyn analyzer cấm log raw PSID không?** — defense in depth (nice-to-have)
4. **Backup destination** — Wasabi, Backblaze, Google Drive API, hoặc skip backup?
5. **VPS hiện tại có đủ RAM cho Seq?** — cần verify spec VPS production trước khi deploy
6. **Disk space hiện tại + ước lượng cho 14 ngày Seq data** — cần monitor sau deploy
