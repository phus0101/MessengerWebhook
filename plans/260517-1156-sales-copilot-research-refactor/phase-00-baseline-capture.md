# Phase 00: Baseline Metrics Capture

**Priority**: P0 (prerequisite)
**Effort**: 1 ngày (1 ngày elapsed wait + 0.5 ngày work)
**Status**: In Progress — templates ready, awaiting production data fill-in
**Depends on**: Production Stabilization Phase 02 đã deploy (đã COMPLETED 2026-05-13)

---

## Vấn đề

Plan refactor đặt nhiều success criteria so sánh "trước/sau" (cache hit rate, regression %, latency cải thiện), nhưng:
- `plans/reports/` không có `baseline-*.md`
- Phase 02 Production Stabilization defer baseline report → "2026-05-20+"
- 5/17 hôm nay → cần capture trước khi bắt đầu Phase 01 thực sự để có ground truth

Không có baseline = không thể prove "improvement" sau refactor.

---

## Mục tiêu

1. **Run 5 Seq saved queries** (đã có từ Phase 02 stabilization) export ra số liệu thực
2. **Capture cost baseline** — Gemini token usage / 1k webhook + Pinecone query count
3. **Snapshot 6 critical metrics** vào 1 markdown report duy nhất
4. **Lock baseline trước Phase 01** — mọi PR sau Phase 01 ref về số này

---

## 6 Metrics cần capture

| Metric | Source | Target after refactor |
|---|---|---|
| Webhook completion p50/p95/p99 (per Path) | Seq `Webhook completed` events | Phase 04 reduce by ≥ 15% |
| Error rate (1h, 24h, 7d) | Seq `@Level in ['Error','Fatal']` | Phase 02 reduce by ≥ 50% in degraded scenarios |
| Gemini API call latency p95 | Seq `AICallCompleted` | Phase 02 unchanged (resilience adds <50ms overhead) |
| Cache hit rate per Layer (Embedding, Result) | Seq `CacheLookup` events | Phase 03 add new SemanticAnswer layer ≥ 50% |
| Conversation length distribution | Histogram of history.Count per session | Phase 04 reduce avg token cost ≥ 30% |
| Cost per 1000 webhook | Gemini API billing × 1k / actual count | Phase 03+04 reduce ≥ 20% |

---

## Files cần tạo

- `plans/reports/baseline-260517-prod-snapshot.md` — số liệu thực
- `plans/reports/baseline-260517-cost-estimate.md` — cost breakdown

---

## Implementation Steps

### Step 1: Verify Seq có ≥ 3 ngày data (0.1 ngày)

```
@Application = 'MessengerWebhook' and @Timestamp > now() - 7d
| summarize Count = count() by bin(@Timestamp, 1d)
```

Yêu cầu: tối thiểu 3 ngày liên tục. Nếu < 3 ngày → wait, không bắt đầu refactor.

### Step 2: Export 5 Seq queries (0.25 ngày)

Run các query từ `plans/260508-1039-production-stabilization/phase-02-baseline-and-alerts.md` Step 3:
1. Latency percentile per path (last 24h + last 7d)
2. Top 10 outlier tenant theo p95
3. Error rate hourly
4. Cache hit rate per layer
5. Gemini latency p95 hourly

Export kết quả → tables trong `baseline-260517-prod-snapshot.md`.

### Step 3: Cost estimate (0.15 ngày)

```
# Gemini Console → Billing
# Lấy số token input/output 7 ngày gần nhất
# Chia cho số webhook trong cùng period (từ Seq)
```

Format:
```
- Gemini input tokens/webhook: avg 1200 (p95 2800)
- Gemini output tokens/webhook: avg 180 (p95 350)
- Cost/1000 webhook: $X (Gemini) + $Y (Pinecone) = $Z total
```

### Step 4: Conversation length histogram (0.1 ngày)

Seq query mới:
```
@MessageTemplate startswith 'Webhook completed'
| extend HistoryCount = HistoryCount  // nếu chưa log → skip
| summarize Count = count() by bin(HistoryCount, 2)
```

**Nếu HistoryCount chưa được log** → thêm log statement trong `SalesStateHandlerBase.HandleAsync` (1 dòng code, push hot fix trước Phase 00), wait 24h re-query.

### Step 5: Lock baseline (0.0 ngày)

Commit `baseline-260517-prod-snapshot.md` vào git. PR title: `docs: lock baseline metrics for refactor 260517`.

Mọi phase sau ref về commit hash này khi report "improvement".

---

## Todo

- [ ] Verify Seq có ≥ 3 ngày data
- [ ] Run + export 5 Seq queries → điền vào `plans/reports/baseline-260517-prod-snapshot.md`
- [ ] Tính cost/1000 webhook từ Gemini Console → điền vào `plans/reports/baseline-260517-cost-estimate.md`
- [x] Capture conversation length histogram — `HistoryCount` property thêm vào `SalesHandlerCompleted` event (commit `3e213de`)
- [x] Tạo `baseline-260517-prod-snapshot.md` template với Seq queries sẵn sàng
- [x] Tạo `baseline-260517-cost-estimate.md` template với cost breakdown structure
- [ ] Điền số liệu thực vào 2 files sau khi có Seq + Gemini Console access
- [ ] Commit + tag baseline (sau khi có số liệu thực)

---

## Success Criteria

- 2 baseline report committed vào `plans/reports/`
- 6 metrics có số cụ thể (không bullet "TBD")
- Mỗi phase sau (01-07) reference baseline khi nêu "improvement"

---

## Risk

- **Seq data < 3 ngày**: Wait, không skip — số liệu < 3 ngày không đại diện
- **HistoryCount chưa log**: 1 hour patch + 24h wait là acceptable, không block Phase 07 (csproj cleanup chạy parallel)
- **Cost data lag từ Gemini billing**: Console có 24h lag → dùng prior week data
