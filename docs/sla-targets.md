# SLA Targets — MessengerWebhook

**Version**: 1.0 (initial)
**Established**: 2026-05-17
**Review cadence**: Quarterly
**Next review**: 2026-08-17

> **Note**: These are initial targets based on architectural estimates. Validate and tighten after 30 days of production data. See [Measurement methodology](#measurement-methodology).

---

## Definitions

| Term | Definition |
|------|-----------|
| **SLO** | Service Level Objective — internal engineering target |
| **SLA** | Service Level Agreement — commitment to customers |
| **Error budget** | (1 − SLO) × period — allowed failure time before action required |
| **p50/p95/p99** | 50th/95th/99th percentile latency |
| **Uptime** | % of minutes with < 1% error rate on health endpoint |
| **Webhook reception** | % of Facebook events acknowledged within 5s (Facebook requirement) |

---

## Internal SLO (engineering targets)

Measured over 5-minute windows. Alerts fire at **90% of threshold** (early warning before breach).

| Metric | SLO Target | Alert threshold | Window |
|--------|-----------|-----------------|--------|
| Webhook ack latency p99 | < 500ms | > 450ms | 5 min |
| Reply latency p50 | < 2s | > 1.8s | 5 min |
| Reply latency p95 (small talk) | < 1s | > 900ms | 5 min |
| Reply latency p95 (RAG) | < 4s | > 3.6s | 5 min |
| Reply latency p95 (overall) | < 5s | > 4.5s | 5 min |
| Reply latency p99 | < 10s | > 9s | 5 min |
| HTTP 5xx error rate | < 0.5% | > 0.45% | 5 min |
| Webhook uptime | ≥ 99.95% | < 99.95% | 30 days |
| Message dropped (channel pressure) | 0 | ≥ 1 | per minute |

---

## Customer SLA (external commitments)

Measured over calendar month. Breaches trigger credit per the penalty column.

| Metric | SLA Target | Window | Penalty |
|--------|-----------|--------|---------|
| Service uptime | ≥ 99.9% | Monthly | Credit proportional to downtime |
| Webhook reception | ≥ 99.95% | Monthly | Credit |
| Reply latency p95 | < 7s | Monthly | Best effort — no penalty |

### Uptime credit table

| Monthly uptime | Credit |
|----------------|--------|
| 99.0% – 99.9% | 10% of monthly fee |
| 95.0% – 99.0% | 25% of monthly fee |
| < 95.0% | 50% of monthly fee |

### Exclusions

The following are excluded from uptime calculation:
- Scheduled maintenance (announced ≥ 24h in advance)
- Facebook platform outages (documented at Meta status page)
- Force majeure (natural disaster, government action)
- Tenant-caused issues (invalid tokens, webhook misconfiguration)

---

## Business SLO (monitored, no penalty)

| Metric | Target | Window | Notes |
|--------|--------|--------|-------|
| Hallucination rate | < 0.1% | 7 days | Manual spot-check, not automated |
| Draft order duplicate rate | < 0.05% | 7 days | Measured via DB unique constraint violations |
| Tenant cross-leak incidents | 0 | All time | Zero tolerance — triggers postmortem |
| Embedding cache hit rate | > 80% | 24h | Log field: `CacheHit=true` on embedding lookup |
| Result cache hit rate | > 60% | 24h | Log field: `CacheHit=true` on response lookup |

---

## Cost SLO

> Baseline not yet established. Fill in after 30 days production.

| Metric | Target | Notes |
|--------|--------|-------|
| Gemini cost / active conversation | < $0.02 | TBD — measure via Gemini billing dashboard |
| Pinecone cost / 1000 search | < $0.50 | TBD |
| Total cost / tenant / month | TBD | Establish after 30-day data |

---

## Error budget policy

### Calculation

```
Error budget (monthly) = (1 − SLO) × 43,800 minutes
99.95% uptime → budget = 21.9 min/month
99.9% uptime  → budget = 43.8 min/month
0.5% error rate → budget = 219 min × (requests/min)
```

### Policy

| Budget consumed | Action |
|----------------|--------|
| ≥ 50% in first 15 days | Freeze non-critical feature deploys, focus on stability |
| ≥ 75% any point | Mandatory incident review, no new deploys without approval |
| 100% | Postmortem required, customer disclosure if SLA breach |

---

## Measurement methodology

### Tools
- **Latency**: Serilog structured logs → Seq → query `SalesHandlerCompleted`, `MessageSent` events
- **Error rate**: Seq alert query `@Level in ['Error','Fatal']` count/5 min
- **Uptime**: Health endpoint `/healthz` check (external monitor or Seq alert on no-heartbeat)
- **Dropped messages**: Log event `MessageDropped` count/min

### Key log events
| Event | Log field | SLO |
|-------|-----------|-----|
| `SalesHandlerCompleted` | `ElapsedMs` | Reply latency |
| `WebhookAcknowledged` | `ElapsedMs` | Webhook ack latency |
| `MessageDropped` | — | Drop rate |
| `WebhookError` | — | Error rate |

### Percentile queries (Seq)

```
# Reply latency p95 (5-min window)
select percentile(ElapsedMs, 95)
from stream
where @MessageTemplate like '%SalesHandlerCompleted%'
  and @Timestamp > Now() - 5m
```

---

## Baseline review schedule

| Date | Action |
|------|--------|
| **2026-06-17** (30 days after v1.0) | Pull p50/p95/p99 from Seq, validate SLO targets |
| **2026-08-17** (Q3 review) | Full quarterly review, update targets if needed |
| **2026-11-17** (Q4 review) | Annual review, consider customer SLA upgrades |

---

## Breach response

See [Runbook: SLA Breach Response](runbooks/sla-breach-response.md) for step-by-step process.

See also:
- [Runbook: Webhook 5xx Burst](runbooks/alert-response-webhook-5xx.md)
- [Runbook: High Latency](runbooks/alert-response-high-latency.md)
- [Runbook: Message Dropped](runbooks/alert-response-message-dropped.md)
