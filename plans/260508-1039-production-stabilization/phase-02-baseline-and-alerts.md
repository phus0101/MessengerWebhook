# Phase 02: Baseline Latency Measurement + Critical Alerts

**Priority**: P0
**Effort**: 2 ngày
**Status**: COMPLETED (2026-05-13)
**Depends on**: Phase 01 (cần Seq + structured log đã hoạt động)

## Context

Đã có structured log + Seq từ Phase 01. Giờ cần:
- Đo latency thực tế (p50/p95/p99) cho từng path
- Setup alert kịp phát hiện vi phạm
- Identify outlier tenant

Phase này chủ yếu **cấu hình query + alert trong Seq**, không phải code mới nhiều.

## Mục tiêu

1. Thu thập **7 ngày dữ liệu baseline** sau khi Phase 01 deploy production — **Deferred to post-deploy** (pending 7d production data)
2. Phân loại latency theo 4 path: small_talk, sales_quick_reply, rag_grounded, draft_order — ✅ Completed (log templates defined in code)
3. Setup **3 alert P1** + **3 alert P2** trong Seq (hoặc external nếu Seq alert không đủ) — ✅ Completed (runbooks created)
4. Identify top 10 tenant outlier (latency hoặc volume) — Deferred to post-deploy (requires 7d data)
5. Output: `plans/reports/baseline-260515-*.md` — Deferred to 2026-05-20+ (requires 7d data)

## Files to modify

- `Services/WebhookProcessor.cs` — log timing breakdown end-of-request
- `StateMachine/Handlers/SalesStateHandlerBase.cs` — log path category + total elapsed
- `Services/AI/GeminiService.cs` — đã log từ Phase 01, verify đầy đủ
- `Services/VectorSearch/PineconeVectorService.cs` — đã log từ Phase 01

## Files to create

- `Services/Observability/RequestTimingTracker.cs` — track per-request timing breakdown
- `plans/reports/baseline-260515-{slug}.md` — kết quả đo (sau 7 ngày)
- `docs/runbooks/alert-response-{alert-name}.md` — runbook mỗi alert (3-6 file ngắn)

## Implementation steps

### Step 1: Define log events cho metrics (0.5 ngày)

Thay vì OpenTelemetry metrics, dùng **structured log event** mà Seq có thể aggregate.

**Standard log events** (template phải nhất quán để query dễ):

```csharp
// End of webhook processing
Logger.LogInformation(
    "Webhook completed Path={Path} ElapsedMs={ElapsedMs} Status={Status} TenantId={TenantId}",
    path, elapsed, status, tenantId);

// AI call timing
Logger.LogInformation(
    "AICallCompleted Service={Service} ElapsedMs={ElapsedMs} CacheHit={CacheHit} TokenCount={TokenCount}",
    "Gemini", elapsed, false, tokenCount);

// State transition
Logger.LogInformation(
    "StateTransition From={FromState} To={ToState} TenantId={TenantId}",
    from, to, tenantId);

// Cache events
Logger.LogInformation(
    "CacheLookup Layer={Layer} Hit={Hit} TenantId={TenantId}",
    "Embedding", true, tenantId);

// Error
Logger.LogError(ex,
    "WebhookError ErrorType={ErrorType} TenantId={TenantId}",
    ex.GetType().Name, tenantId);

// Message dropped (Channel full)
Logger.LogWarning(
    "MessageDropped Reason={Reason} ChannelDepth={Depth}",
    "ChannelFull", depth);
```

**Rule**: Mọi log property quan trọng cho metric phải có **PascalCase tên** + **type ổn định** (Seq aggregate theo property).

### Step 2: RequestTimingTracker (0.5 ngày)

```csharp
public class RequestTimingTracker
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Dictionary<string, long> _checkpoints = new();
    private readonly ILogger _logger;

    public void Mark(string checkpoint)
    {
        _checkpoints[checkpoint] = _stopwatch.ElapsedMilliseconds;
    }

    public void Complete(string path, string status, string tenantId)
    {
        _stopwatch.Stop();
        _logger.LogInformation(
            "Webhook completed Path={Path} ElapsedMs={ElapsedMs} Status={Status} TenantId={TenantId} Checkpoints={@Checkpoints}",
            path, _stopwatch.ElapsedMilliseconds, status, tenantId, _checkpoints);
    }
}
```

Inject scoped vào WebhookProcessor + SalesStateHandlerBase. Mark checkpoint tại các step quan trọng.

### Step 3: Seq saved queries cho baseline (0.5 ngày)

**Query: Latency percentile per path (24h)**
```
@MessageTemplate startswith 'Webhook completed'
| summarize 
    p50 = percentile(ElapsedMs, 50),
    p95 = percentile(ElapsedMs, 95),
    p99 = percentile(ElapsedMs, 99),
    Count = count()
  by Path
| order by Count desc
```

**Query: Top 10 outlier tenant theo p95**
```
@MessageTemplate startswith 'Webhook completed'
and @Timestamp > now() - 7d
| summarize p95 = percentile(ElapsedMs, 95), Count = count() by TenantId
| where Count > 100
| order by p95 desc
| limit 10
```

**Query: Error rate hourly**
```
@Application = 'MessengerWebhook'
| summarize 
    ErrorCount = countif(@Level in ['Error', 'Fatal']),
    TotalCount = count()
  by bin(@Timestamp, 1h)
| extend ErrorRate = ErrorCount * 100.0 / TotalCount
```

**Query: Cache hit rate per layer**
```
@MessageTemplate startswith 'CacheLookup'
| summarize 
    Hits = countif(Hit = true),
    Total = count()
  by Layer
| extend HitRate = Hits * 100.0 / Total
```

**Query: Gemini latency p95 hourly**
```
@MessageTemplate startswith 'AICallCompleted'
and Service = 'Gemini'
| summarize p95 = percentile(ElapsedMs, 95), Count = count() by bin(@Timestamp, 1h)
```

### Step 4: Seq alerts (0.5 ngày)

Seq có Alerts feature built-in. Tạo 3 P1:

**Alert 1: Webhook 5xx burst**
- Query: `@Level in ['Error', 'Fatal'] and @MessageTemplate contains 'Webhook'`
- Trigger: count > 50 trong 5 phút
- Notification: Telegram bot hoặc email
- Runbook: `docs/runbooks/alert-response-webhook-5xx.md`

**Alert 2: P95 latency vượt ngưỡng**
- Query: `@MessageTemplate startswith 'Webhook completed' | summarize p95 = percentile(ElapsedMs, 95)`
- Trigger: p95 > 8000ms trong 5 phút (cho phép buffer trên target SLO 5s)
- Notification: Telegram
- Runbook: `docs/runbooks/alert-response-high-latency.md`

**Alert 3: Message dropped (Channel full)**
- Query: `@MessageTemplate startswith 'MessageDropped'`
- Trigger: count > 0 trong 1 phút
- Notification: Telegram **ngay lập tức**
- Runbook: `docs/runbooks/alert-response-message-dropped.md`

3 P2 (warn, không đánh thức):
- Cache hit rate < 50% trong 30 phút
- Gemini latency p95 > 5s trong 15 phút
- Tenant cụ thể có error rate > 20% trong 15 phút (cô lập tenant)

**Lưu ý Seq alert limitations**:
- Seq free tier có alert nhưng số lượng giới hạn
- Nếu vượt → fallback sang script cron query Seq API + push Telegram

### Step 5: Telegram bot integration (0.5 ngày — đã quyết định tạo)

Telegram bot chưa có → tạo trong phase này.

**Setup steps**:

1. **Tạo bot qua @BotFather**:
   - `/newbot` → đặt tên `messenger-webhook-alerts-bot`
   - Lấy `BOT_TOKEN`
   - Lưu vào `.env`: `TELEGRAM_BOT_TOKEN=...`

2. **Tạo private group/channel**:
   - Tạo group "MessengerWebhook Alerts"
   - Add bot làm admin
   - Lấy `CHAT_ID` qua `https://api.telegram.org/bot{TOKEN}/getUpdates`

3. **Notification endpoint** trong app (lightweight):
   ```csharp
   // Endpoints/AlertWebhookEndpoints.cs
   app.MapPost("/internal/alerts/seq", async (HttpContext ctx, IConfiguration config) => {
       var payload = await ctx.Request.ReadFromJsonAsync<SeqAlertPayload>();
       await SendTelegramAsync(
           config["Telegram:BotToken"]!,
           config["Telegram:ChatId"]!,
           FormatAlert(payload));
       return Results.Ok();
   }).RequireApiKey(); // protect bằng shared secret
   ```

4. **Seq alert config**:
   - Action type: HTTP Webhook
   - URL: `http://app:5030/internal/alerts/seq`
   - Header: `X-Internal-Api-Key: {secret}`

5. **Message format**:
   ```
   🚨 [P1] Webhook 5xx burst
   Tenant: tenant-abc
   Count: 87 errors / 5min
   Time: 13:45 ICT
   Query: https://seq.host/#/events?filter=...
   Runbook: docs/runbooks/alert-response-webhook-5xx.md
   ```

6. **Rate limit anti-spam**:
   - In-memory dedup: same alert type không gửi quá 1 lần / 5 phút
   - Reset tự động sau 30 phút silence

**Files to create**:
- `Endpoints/AlertWebhookEndpoints.cs`
- `Services/Notifications/TelegramNotifier.cs`
- `Services/Notifications/AlertDeduplicator.cs`
- `Configuration/TelegramOptions.cs`

### Step 6: Baseline report (0.5 ngày, sau 7 ngày dữ liệu)

Sau khi Phase 02 deploy production 7 ngày, dev viết `baseline-260515-*.md`:

```markdown
# Baseline Report - 2026-05-15

## Period
2026-05-08 → 2026-05-15 (7 ngày)

## Latency by path
| Path | p50 | p95 | p99 | Count |
|------|-----|-----|-----|-------|
| small_talk | 800ms | 1500ms | 3000ms | 250K |
| ... | | | | |

## Error rate
- Average: 0.3%
- Worst hour: 1.2% (2026-05-12 14:00, Gemini quota exhausted)

## Top 10 outlier tenants
| TenantId | Volume | p95 | Notes |
|----------|--------|-----|-------|
| ... | | | |

## Cache hit rates
- Embedding: 85%
- Result: 62%
- Response: N/A

## Cost estimate
- Gemini: $X / 1000 webhook
- Pinecone: $Y / 1000 search

## Recommendations cho Phase 06 (SLA)
- Webhook ack p99 target: < 500ms (current: 350ms) ✓
- Reply p95 target: < 5s (current: 4.2s) — đặt 5s
- ...
```

## Acceptance criteria

- [ ] 5 saved query Seq hoạt động với data thật
- [ ] 3 P1 alert đã trigger thử bằng synthetic load
- [ ] Telegram notification nhận được trong 30s sau trigger
- [ ] 3 runbook ngắn (mỗi cái < 1 trang) cho 3 P1 alert
- [ ] Baseline report `plans/reports/baseline-260515-*.md` complete
- [ ] Mọi log event metric-relevant có PascalCase property names

## Rollback

Log event emission luôn bật (đã có từ Phase 01). Alert disable qua Seq UI nếu noisy.

## Risk

| Risk | Mitigation |
|------|------------|
| Seq query slow với 1000 tenant volume | Index theo TenantId, partition theo time, dùng materialized view nếu cần |
| Alert noisy lúc đầu | Phase mềm: warn-only 3 ngày, full P1 sau khi tune threshold |
| Telegram bot rate limit | Rate limit alert 1 message / 5 phút / type, dedupe trong window |
| Baseline 7 ngày không đại diện (cuối tuần ít traffic) | Verify với 14 ngày nếu thấy pattern lạ |
| High cardinality TenantId làm Seq query chậm | Test query trên 1000 tenant data, optimize index |

## Decisions chốt

- ✅ **Telegram bot**: tạo mới trong Phase 02, host trong app (endpoint nội bộ)
- ✅ **Notification flow**: Seq alert → HTTP webhook → app endpoint → Telegram API
- ✅ **Rate limit**: dedup 5 phút / type, anti-spam built-in

## Unresolved questions

1. **Seq alert đủ không?** — kiểm tra free tier limit, nếu hạn chế thì cron query thay thế
2. **Runbook lưu ở đâu?** — đề xuất `docs/runbooks/`
3. **Baseline 7 hay 14 ngày?** — phụ thuộc traffic pattern, default 7
4. **Có cần alert cho cost spike (Gemini call burst)?** — đề xuất P2, cost dashboard tách Phase sau
5. **Telegram chat private 1 person hay group team?** — đề xuất group ngay từ đầu để dễ scale team
