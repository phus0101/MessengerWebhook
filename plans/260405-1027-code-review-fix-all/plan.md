---
title: "Fix All Code Review Issues - Full Codebase"
description: "Address 13 issues from code review: 4 critical, 5 high, 4 medium, 5 low"
status: pending
priority: P1
effort: ~15h
branch: master
tags: [code-review, refactoring, security, quality]
created: 2026-04-05
---

# Plan: Fix All Code Review Issues

## Dependency Graph

```
Phase 0 (Critical) ──┬──► Phase 1 (C2 refactor) ──► Phase 5 (H5 typed state)
                     ├──► Phase 2 (C3 race condition)
                     ├──► Phase 3 (C4 token header) ──► Phase 6 (H1 PII redaction)
                     └──► Phase 4 (H2/H3 channel + concurrency)
Phase 5 (H5 typed state) ──► Phase 7 (H4 prompt injection + M1 dedup + M3 TenantId on Order + M4 pagination)
All phases ──► Phase 8 (LOW items + test updates + compile)
```

## Phase Status

| # | Phase | Status | Progress |
|---|-------|--------|----------|
| 0 | Enable config validation | Not started | 0% |
| 1 | Extract DraftOrderCoordinator service | Not started | 0% |
| 2 | Consolidate FacebookPageConfig creation | Not started | 0% |
| 3 | Move access token to Bearer header | Not started | 0% |
| 4 | Channel drop logging + live comment concurrency | Not started | 0% |
| 5 | Typed ConversationState model | Not started | 0% |
| 6 | PII log redaction | Not started | 0% |
| 7 | Prompt injection + dedup queries + TenantId + pagination | Not started | 0% |
| 8 | Low-priority fixes, test updates, compile | Not started | 0% |

## Key Dependencies

- `Microsoft.Extensions.Options.DataAnnotations` (for IValidateOptions, bundled with .NET 8)
- `System.Threading.Channels` (already in use)
- EF Core migrations for C3 (unique index) and M3 (TenantId column on Order)

## Risk Summary

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| DraftOrderCoordinator refactoring breaks state machine | Medium | High | Preserve existing behavior 1:1, test all 4 callers through existing unit tests |
| EF migration adds NotNull TenantId to Order with existing data | Medium | Medium | Use nullable column with backfill migration, then enforce in v2 |
| IValidateOptions breaks dev setup where keys are missing | Low | Medium | Keep conditional: skip in Development env with explicit opt-in flag |
| Bearer token change breaks Facebook Graph API calls | Low | High | Facebook docs confirm Bearer is preferred method, but test all 5 call sites |

---

## Phase Detail Cross-Reference

- [Phase 0: Enable Config Validation](phase-00-enable-config-validation.md) — C1
- [Phase 1: Extract DraftOrderCoordinator Service](phase-01-extract-draft-order-coordinator.md) — C2
- [Phase 2: Consolidate PageConfig Creation](phase-02-consolidate-pageconfig-creation.md) — C3
- [Phase 3: Move Access Token to Bearer Header](phase-03-bearer-token-header.md) — C4
- [Phase 4: Channel Logging and Comment Concurrency](phase-04-channel-and-concurrency.md) — H2, H3
- [Phase 5: Typed State Model](phase-05-typed-state-model.md) — H5
- [Phase 6: PII Log Redaction](phase-06-pii-log-redaction.md) — H1
- [Phase 7: Prompt Injection, Tenant Dedup, Order TenantId, Pagination](phase-07-bulk-medium-fixes.md) — H4, M1, M3, M4
- [Phase 8: Low-Priority Fixes and Compilation](phase-08-low-priority-fixes.md) — L1-L5
