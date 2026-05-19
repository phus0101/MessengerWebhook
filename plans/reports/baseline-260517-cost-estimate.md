---
name: baseline-260517-cost-estimate
description: Cost baseline per 1000 webhooks trước khi refactor (Gemini + Pinecone + Redis)
metadata:
  type: baseline
  captured_at: 2026-05-17
  status: pending-data  # Điền số liệu từ Gemini Console billing page
  reference_commit: 3e213de
---

# Baseline Cost Estimate — 2026-05-17

> **Mục đích:** Đo cost per 1000 webhooks trước refactor. Sau Phase 03+04 expect giảm ≥ 20%.
> **Ref commit:** `3e213de`
> **Status:** Template — điền số liệu từ Gemini Console + Pinecone Dashboard

---

## Pricing Reference (2026-05)

### Gemini 2.5 Flash (default model trước Phase 08)
| Token type | Price |
|------------|-------|
| Input (non-cached) | $0.30 / 1M tokens |
| Output | $2.50 / 1M tokens |

### Gemini 2.5 Pro (escalated queries)
| Token type | Price |
|------------|-------|
| Input | $1.25 / 1M tokens |
| Output | $10.00 / 1M tokens |

### Gemini 2.5 FlashLite
| Token type | Price |
|------------|-------|
| Input | ~$0.10 / 1M tokens |
| Output | ~$0.40 / 1M tokens |

### Pinecone
- Query cost: $0.08 / 1M queries (serverless)
- Upsert: $0.02 / 1M vectors

---

## Data Collection Steps

### Bước 1: Gemini Console → AI Studio / Google Cloud Billing
URL: `console.cloud.google.com/billing` → chọn project → Filter: Vertex AI / Generative AI

Lấy từ prior 7 ngày:
- Tổng input tokens
- Tổng output tokens
- Tổng số API calls

### Bước 2: Seq webhook count
```
@Application = 'MessengerWebhook'
  and @MessageTemplate startswith 'WebhookCompleted'
  and @Timestamp > now() - 7d
| summarize Count = count()
```

### Bước 3: Pinecone Dashboard
URL: `app.pinecone.io` → Index stats → Operations (7d)

---

## Baseline Numbers

### Gemini Usage (7 ngày gần nhất)

| Metric | Value |
|--------|-------|
| Total input tokens | _TBD_ |
| Total output tokens | _TBD_ |
| Total API calls | _TBD_ |
| Webhooks trong cùng period | _TBD_ |
| **Avg input tokens/webhook** | _TBD_ |
| **Avg output tokens/webhook** | _TBD_ |
| p95 input tokens/webhook | _TBD_ |
| p95 output tokens/webhook | _TBD_ |

### Cost per 1000 Webhooks (Breakdown)

| Component | Formula | Cost/1k webhook |
|-----------|---------|----------------|
| Gemini input (Flash) | (avg_input_tokens / 1M) × $0.30 × 1000 | _TBD_ |
| Gemini output (Flash) | (avg_output_tokens / 1M) × $2.50 × 1000 | _TBD_ |
| Pinecone queries | ~1 query/webhook × ($0.08/1M) × 1000 | ~$0.00008 |
| **Total** | | **_TBD_** |

### Estimated Monthly Cost (1000 tenants baseline)

| Metric | Value |
|--------|-------|
| Avg webhooks/tenant/day | _TBD_ |
| Total webhooks/month (est.) | _TBD_ |
| **Estimated monthly Gemini cost** | **_TBD_** |

---

## Improvement Targets

| Phase | Lever | Expected savings |
|-------|-------|-----------------|
| Phase 03 (Semantic Cache) | 50%+ PolicyQuestion/ShippingQuestion cache hit → skip LLM | ~20% token reduction |
| Phase 04 (Context Window) | Summary replaces long history → fewer input tokens | ~30% input token reduction |
| Phase 08 (Model Routing) | 70%+ requests use Flash/FlashLite instead of Pro | ~40% cost reduction vs Pro |
| **Combined target** | | **≥ 40% cost/webhook reduction** |

---

## Post-Refactor Comparison (Fill in after 7 days)

| Metric | Baseline (pre-refactor) | Post-refactor | Δ |
|--------|------------------------|---------------|---|
| Avg input tokens/webhook | _TBD_ | — | — |
| Avg output tokens/webhook | _TBD_ | — | — |
| Cost/1k webhook | _TBD_ | — | — |
| SemanticAnswer cache hit rate | N/A | — | — |
| Flash % of requests | 0% (all one tier) | — | — |

---

## Notes

- Gemini Console billing has 24h lag — use prior week data for baseline
- If model usage not broken down by model in Console: check `AICallCompleted` Seq events for `Model` property
- Phase 08 cost savings only measurable after ≥ 7 days production traffic with new routing
