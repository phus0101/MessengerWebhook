# Production Stabilization Plan

**Created**: 2026-05-08
**Updated**: 2026-05-09 (Phase R-04 completed)
**Context**: 1000 tenant production, team 2 (1 dev + Claude Code), chưa có SLA
**Scope**: Tier 0 + Refactor + Tier 1 (10-12 tuần)

---

## Mục tiêu

1. **Hết mù vận hành** — biết chuyện gì đang xảy ra với 1000 tenant
2. **Đặt SLA dựa trên dữ liệu thật** — không đoán
3. **Refactor `SalesStateHandlerBase`** an toàn (sau khi có observability)
4. **Khóa các lỗ hổng nguy hiểm** từ review cũ chưa fix (C3, C4, H1)
5. **Verify tenant isolation** — zero tolerance cho data leak
6. **Quick win Program.cs** — giảm friction merge conflict

## Ràng buộc

- 2 người: 1 dev + Claude Code → mọi phase phải **deploy được độc lập**
- 1000 tenant đang chạy → mọi thay đổi phải **backward compatible**, có rollback path
- Refactor SalesStateHandlerBase chỉ thực hiện sau khi có observability + golden test suite
- Không pause feature development trên 10 tuần
- Token efficiency: ưu tiên dùng tool có sẵn

## Phases

### Foundation (bắt buộc trước refactor)

| # | Tên | Effort | Status | Phụ thuộc |
|---|-----|--------|--------|-----------|
| 01 | [Structured logging + Seq + correlation ID](phase-01-observability.md) | 2 ngày | Pending | None |
| 02 | [Baseline latency measurement + alerts](phase-02-baseline-and-alerts.md) | 2 ngày | Pending | Phase 01 |

**Backend chosen**: Seq (self-host Docker, ~$10/tháng VPS hoặc free local dev)

### Refactor SalesStateHandlerBase (15 ngày, 5 sub-phase)

| # | Tên | Effort | Status | Phụ thuộc |
|---|-----|--------|--------|-----------|
| R-01 | [Golden conversation test suite + bridge coverage gap](phase-r01-golden-test-suite.md) | 3 ngày | Complete | Phase 02 |
| R-02 | [Extract SalesContextResolver + SalesPromptBuilder](phase-r02-extract-context-and-prompt.md) | 4 ngày | Complete | R-01 |
| R-03 | [Extract ContactConfirmationFlow](phase-r03-extract-contact-flow.md) | 4 ngày | Complete | R-02 |
| R-04 | [Extract SalesReplyOrchestrator](phase-r04-extract-reply-orchestrator.md) | 3 ngày | Complete | R-03 |
| R-05 | [Slim base class + final cleanup](phase-r05-slim-base-class.md) | 2 ngày | Pending | R-04 |

### Stabilization (sau refactor)

| # | Tên | Effort | Status | Phụ thuộc |
|---|-----|--------|--------|-----------|
| 03 | [Critical fixes: race + token leak + PII log](phase-03-critical-fixes.md) | 2 ngày | Pending | Phase 01 |
| 04 | [Tenant isolation audit](phase-04-tenant-isolation-audit.md) | 2-3 ngày | Pending | Phase 01 |
| 05 | [Program.cs modularization](phase-05-program-cs-split.md) | 1 ngày | Pending | None |
| 06 | [Define & publish SLA](phase-06-define-sla.md) | 0.5 ngày | Pending | Phase 02 |

**Tổng effort**: ~26-28 ngày dev (~11-13 tuần wall-clock với 1 dev + buffer incident)

## Thứ tự thực thi (Option B)

```
Tuần 1:    Phase 01 (observability)             ← đèn pin trước khi vào hang
Tuần 2:    Phase 02 (baseline + alert)          ← biết hành vi hiện tại
Tuần 3:    R-01 (golden test suite)             ← safety net trước khi refactor
Tuần 4-5:  R-02 (extract context + prompt)
Tuần 6-7:  R-03 (extract contact flow)
Tuần 8:    R-04 (extract reply orchestrator)
Tuần 9:    R-05 (slim base class)
Tuần 10:   Phase 05 (Program.cs split, quick win)
Tuần 11:   Phase 03 (critical fixes)
Tuần 12:   Phase 04 (tenant isolation audit)
Tuần 13:   Phase 06 (SLA) + buffer
```

**Lý do thứ tự**:
- Phase 01+02 là tiên quyết: refactor không có trace + alert = mù
- R-01 (golden test) bắt buộc trước R-02..R-05 — không có nó refactor sẽ break behavior
- Refactor xong → ổn định → sau đó audit & critical fixes
- Phase 03+04 đặt sau refactor vì code đã sạch, audit dễ hơn
- Phase 05 (Program.cs) đặt giữa để chuyển ngữ cảnh nghỉ giữa refactor lớn và audit
- Phase 06 (SLA) cuối cùng vì cần dữ liệu sau refactor để đặt target chính xác

## Success criteria toàn plan

### Foundation
- [ ] Mọi webhook request có correlation ID, traceable end-to-end
- [ ] Dashboard p50/p95/p99 latency theo từng path
- [ ] 3 alert P1 hoạt động: 5xx burst, p95 latency, message dropped

### Refactor
- [ ] `SalesStateHandlerBase` ≤ 400 dòng (từ 2425)
- [ ] Tách thành 4 service: `SalesContextResolver`, `SalesPromptBuilder`, `ContactConfirmationFlow`, `SalesReplyOrchestrator`
- [ ] Golden test suite ≥ 100 conversation, pass 100% trước & sau mỗi sub-phase
- [ ] 0 regression latency p95 (so với baseline Phase 02)
- [ ] 0 increase error rate (so với baseline)

### Stabilization
- [ ] 0 query EF Core không có TenantId filter
- [ ] C3, C4, H1 fixed
- [ ] Program.cs < 100 dòng
- [ ] SLA document published nội bộ

## Risk & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Refactor break sales conversation | Mất đơn, churn 1000 tenant | Golden test + canary deploy 1 tenant 24h trước rollout |
| OTel overhead làm chậm production | Latency tăng | Sampling 10% lúc đầu |
| Audit isolation phát hiện leak đã xảy ra | Data exposure | Postmortem + disclosure nếu xác định |
| Refactor cần thay đổi DB schema | Migration risk | Cố gắng chỉ refactor in-memory; nếu cần migration → tách phase riêng |
| Sub-phase R-XX kéo dài quá 1 tuần | Scope creep | Hard cutoff: nếu phase chưa xong tuần thứ 2 → revert, replan |
| 1 dev burnout với 12 tuần liên tục | Quality drop | Buffer 1 tuần mỗi 4 tuần; phase R-01..R-05 có thể skip 1 tuần nghỉ |

## Decision gates

Trước khi bắt đầu mỗi phase:
1. Phase trước đã pass acceptance criteria
2. Production health green 7 ngày liên tiếp
3. Không có incident P0/P1 đang mở
4. Dev confirm có window deploy phù hợp

**Hard stop trước R-02 (refactor thật sự bắt đầu)**: phải verify Phase 01+02+R-01 đã hoàn tất.

## Unresolved questions

1. ~~APM backend?~~ ✅ **Seq** chosen
2. **Seq deployment**: cùng VPS với app, hay VPS riêng? (Phase 01)
3. **Channel alert?** — Telegram/Slack/Seq alert built-in? (Phase 02)
4. **Có CI/CD rollback nhanh chưa?** — nếu chưa, thêm phase setup trước R-02
5. ~~Test coverage hiện tại của SalesStateHandlerBase là bao nhiêu %?~~ ✅ Đo: line 74%, branch 64%, complexity 445. **CompleteStateHandler** branch 0% là rủi ro cao nhất. R-01 effort tăng 2 → 4 ngày.
6. **Có duplicate FacebookPageId trong production?** (Phase 03)
7. **Background services dùng tenant context thế nào?** (Phase 04)
8. **Pinecone**: namespace hay metadata filter? (Phase 04)
9. **Customer-facing SLA hay nội bộ?** (Phase 06)
10. **Có incident leak nào đã xảy ra chưa?** (Phase 04)
11. **Log retention**: 14 ngày hay 30 ngày trong Seq? (Phase 01)
