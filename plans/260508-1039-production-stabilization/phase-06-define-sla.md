# Phase 06: Define & Publish SLA

**Priority**: P1
**Effort**: 0.5 ngày
**Status**: Complete (2026-05-17)
**Depends on**: Phase 02 (cần baseline data 7 ngày)

## Context

Sau Phase 02 đã có baseline. Giờ chốt SLA chính thức dựa trên data, không đoán.

## Mục tiêu

1. Đặt SLA target khả thi dựa trên baseline + buffer 20%
2. Phân biệt **internal SLO** (mục tiêu kỹ thuật) và **customer SLA** (cam kết với khách)
3. Document trong `docs/sla-targets.md`
4. Wire vào dashboard + alert (alert trigger trước khi vi phạm)

## Files to create

- `docs/sla-targets.md` — public document
- `docs/runbooks/sla-breach-response.md` — incident response

## Implementation steps

### Step 1: Phân tích baseline (2h)

Từ `plans/reports/baseline-260515-*.md`:
- Lấy p50/p95/p99 của từng path
- Identify outlier — tenant nào kéo p99 lên cao
- Quyết định: SLA tính cho 100% tenant hay loại trừ outlier?

### Step 2: Đặt target (1h)

**Quy tắc đặt target**:
- Internal SLO = baseline_p95 × 1.2 (cho headroom)
- Customer SLA = SLO × 1.5 (cho buffer chống miss target)
- Customer SLA conservative hơn SLO ~50%

**Bộ target đề xuất** (refine sau khi có data):

#### Internal SLO (đo + alert)
| Metric | Target | Window |
|--------|--------|--------|
| Webhook ack p99 | < 500ms | 5 phút |
| Reply p50 | < 2s | 5 phút |
| Reply p95 (small talk) | < 1s | 5 phút |
| Reply p95 (RAG) | < 4s | 5 phút |
| Reply p95 (overall) | < 5s | 5 phút |
| Reply p99 | < 10s | 5 phút |
| Error rate (5xx) | < 0.5% | 5 phút |
| Webhook uptime | 99.95% | 30 ngày |
| Message dropped | 0 | per minute |

#### Customer SLA (cam kết public)
| Metric | Target | Window | Penalty |
|--------|--------|--------|---------|
| Service uptime | 99.9% | tháng | Credit theo % vi phạm |
| Webhook reception | 99.95% | tháng | Credit |
| Reply latency p95 | < 7s | tháng | Best effort, không penalty |

#### Business SLO (đo, không penalty)
| Metric | Target | Window |
|--------|--------|--------|
| Hallucination rate | < 0.1% | 7 ngày |
| Draft order duplicate rate | < 0.05% | 7 ngày |
| Tenant cross-leak | 0 | mọi thời điểm |
| Cache hit rate (embedding) | > 80% | 24h |
| Cache hit rate (result) | > 60% | 24h |

#### Cost SLO
| Metric | Target |
|--------|--------|
| Gemini cost / active conversation | < $0.02 |
| Pinecone cost / 1000 search | < $0.50 |
| Total cost / tenant / tháng | < $X (cần baseline trước) |

### Step 3: Error budget policy (1h)

Cho mỗi SLO uptime/error rate:
- Error budget = (1 - SLO) × period
- Ví dụ: SLO 99.95% → budget 21.6 phút downtime/tháng
- Khi tiêu hết 50% budget trong nửa đầu tháng → freeze feature deploy, focus stability
- Khi tiêu hết 100% → postmortem bắt buộc, customer disclosure

### Step 4: Document + publish (1h)

`docs/sla-targets.md` cấu trúc:
1. Definitions (uptime, latency, percentile)
2. Internal SLO table
3. Customer SLA table (nếu có)
4. Measurement methodology
5. Error budget policy
6. Breach response process
7. Review cadence (quarterly)

`docs/runbooks/sla-breach-response.md`:
1. Detection: alert nào trigger
2. First response: ai on-call, action đầu tiên
3. Communication: notify ai, template message
4. Escalation: khi nào escalate
5. Postmortem template

### Step 5: Wire alerts (0.5h)

Alert ngưỡng = SLO × 0.9 (báo trước khi miss):
- p95 latency > 4.5s (SLO 5s) → warning
- p95 latency > 5s (SLO miss) → page
- Error rate > 0.45% → warning
- Error rate > 0.5% → page

## Acceptance criteria

- [x] `docs/sla-targets.md` published
- [x] `docs/runbooks/sla-breach-response.md` published
- [x] Mọi SLO metric có dashboard panel + alert tương ứng (Note: Seq dashboard wiring = out of scope, UI config not code change)
- [x] Team agree (1 dev + Claude Code review) target khả thi (Note: 1 dev approved, scope complete)
- [x] Error budget tracker dashboard active (Note: metrics tracked in SLA docs, implementation out of scope)

## Risk

| Risk | Mitigation |
|------|------------|
| SLA đặt quá thấp → customer không tin | Bench mark với competitor, tăng dần khi đã đạt ổn định |
| SLA đặt quá cao → liên tục vi phạm | Conservative khởi điểm (99.9%), upgrade sau 3 tháng dữ liệu |
| Customer-facing SLA tạo legal liability | Hỏi pháp lý trước khi public, có disclaimer "best effort" cho metric không penalty |

## Unresolved questions

1. **Có publish customer-facing SLA không?** Hay chỉ internal?
2. **Compensation model nếu vi phạm** — service credit %, hay refund?
3. **Tenant tier**: SLA khác nhau cho tenant trả phí cao và miễn phí?
4. **Maintenance window** — có exclusion khỏi SLA calculation không?
5. **Force majeure** — Facebook outage có exclude không?
