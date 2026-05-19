---
name: baseline-260517-prod-snapshot
description: Production metrics snapshot trước khi áp dụng refactor phases 01-10 (2026-05-17)
metadata:
  type: baseline
  captured_at: 2026-05-17
  status: partial  # Webhook latency filled; SalesHandler + Cache + Cost TBD
  reference_commit: 3e213de
---

# Baseline Production Snapshot — 2026-05-17

> **Mục đích:** Ground truth để đo improvement sau refactor phases 01-10.
> **Ref commit:** `3e213de` (trước khi refactor)
> **Status:** Template — điền số liệu từ Seq queries bên dưới

---

## 1. Webhook Latency (p50 / p95 / p99)

Seq query (SQL syntax — pipe-based syntax not supported in this Seq version):
```sql
select @Timestamp, ElapsedMs, Path, Status
from stream
where StartsWith(@Message, 'WebhookCompleted')
  and Application = 'MessengerWebhook'
  and @Timestamp > now() - 7d
order by @Timestamp desc
limit 100
```

| Path | p50 (ms) | p95 (ms) | p99 (ms) | Sample size |
|------|----------|----------|----------|-------------|
| message | **6,100** | **10,750** | **10,750** | 10 (8 real AI, 2 near-instant) |
| quick_reply | — | — | — | 0 |
| postback | — | — | — | 0 |

> Raw ElapsedMs (sorted): 1, 2, 2491, 4540, 6014, 6185, 6393, 7535, 7820, 10750  
> 2 events với 1ms/2ms (2026-05-13) là test/healthcheck — excluded khỏi AI processing baseline  
> AI processing only (n=8): p50 ≈ 6,300ms, p95 ≈ 10,750ms  
> **Error rate: 0%** (10/10 status=ok)

**Target after Phase 04:** p95 giảm ≥ 15%

---

## 2. Error Rate

Seq query:
```sql
select @Timestamp, @Level, @Message
from stream
where Application = 'MessengerWebhook'
  and @Level in ('Error', 'Fatal')
  and @Timestamp > now() - 7d
order by @Timestamp desc
limit 100
```

| Window | Error count | Error rate (% webhook) |
|--------|-------------|------------------------|
| Last 1h | _TBD_ | _TBD_ |
| Last 24h | _TBD_ | _TBD_ |
| Last 7d | _TBD_ | **0%** (inferred from webhook status=ok 10/10) |

**Target after Phase 02:** Error rate giảm ≥ 50% trong degraded scenarios

---

## 3. Gemini API Latency p95

Seq query:
```sql
select @Timestamp, ElapsedMs
from stream
where StartsWith(@Message, 'AICallCompleted')
  and Application = 'MessengerWebhook'
  and @Timestamp > now() - 7d
order by @Timestamp desc
limit 100
```

| Period | p95 (ms) | p50 (ms) |
|--------|----------|----------|
| Last 24h | _TBD_ | _TBD_ |
| Last 7d | _TBD_ | _TBD_ |

**Target after Phase 02:** Unchanged (resilience overhead < 50ms)

---

## 4. Cache Hit Rate

Seq query:
```sql
select @Timestamp, CacheLayer, Hit
from stream
where StartsWith(@Message, 'CacheLookup')
  and Application = 'MessengerWebhook'
  and @Timestamp > now() - 7d
order by @Timestamp desc
limit 100
```

| Cache Layer | Hit rate (%) | Volume/day |
|-------------|-------------|------------|
| EmbeddingCache | _TBD_ | _TBD_ |
| ResultCache | _TBD_ | _TBD_ |
| SemanticAnswer | _N/A (pre-Phase 03)_ | — |

**Target after Phase 03:** SemanticAnswer layer ≥ 50% hit rate

---

## 5. Conversation Length Distribution

Seq query (requires `HistoryCount` property — available after commit `3e213de` deploy):
```sql
select @Timestamp, ElapsedMs, State, HistoryCount
from stream
where StartsWith(@Message, 'SalesHandlerCompleted')
  and Application = 'MessengerWebhook'
  and @Timestamp > now() - 7d
order by @Timestamp desc
limit 100
```

**SalesHandler Latency (AI events >200ms, n=8):**

| Metric | Value |
|--------|-------|
| p50 | **5,434ms** (avg 5283+5586) |
| p95 | **6,413ms** |
| p99 | **6,413ms** (limited sample) |
| Min AI | 212ms |
| Max | 6,413ms |

> Raw (sorted): 212, 3764, 4701, 5283, 5586, 5996, 6340, 6413  
> State mix: Idle, Consulting, CollectingInfo, Complete  
> Near-zero events (0–2ms) excluded — pure state transitions, no AI call

**HistoryCount Distribution** (limited — Phase 00 logging just deployed, n=4):

| HistoryCount bucket | Count | % |
|--------------------|-------|---|
| 0–2 | 3 | 75% |
| 3–4 | 1 | 25% |
| 5+ | 0 | 0% |

**Avg HistoryCount:** ~2.5 (sample too small — need more traffic)  
> Note: majority of events have `HistoryCount=null` (pre-Phase 00 commits) — distribution will be meaningful after 24h+ traffic

**Target after Phase 04:** Avg token cost giảm ≥ 30% (summary replaces long history)

---

## 6. Top-10 Outlier Tenants (p95)

Seq query:
```sql
select @Timestamp, ElapsedMs, TenantId, Path
from stream
where StartsWith(@Message, 'WebhookCompleted')
  and Application = 'MessengerWebhook'
  and @Timestamp > now() - 7d
order by ElapsedMs desc
limit 10
```

| TenantId | p95 (ms) | Volume |
|----------|----------|--------|
| _TBD_ | _TBD_ | _TBD_ |

---

## Notes

- Seq data cần ≥ 3 ngày liên tục trước khi baseline valid
- `HistoryCount` property available sau khi deploy commit `3e213de`
- Gemini cost data có 24h billing lag → dùng prior-week data từ Gemini Console
- Baseline này là ground truth cho tất cả phase improvement claims
