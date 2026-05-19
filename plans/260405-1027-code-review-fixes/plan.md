---
title: "Code Review Fixes - Address All Issues from Full Codebase Review"
description: "Fix 9 critical/high, 4 medium, 5 low issues identified in code review 260405-1012"
status: pending
priority: P1
effort: ~16h
branch: master
tags: [code-review, security, refactoring, quality]
created: 2026-04-05
---

# Code Review Fixes Plan

**Source:** `docs/code-review-260405-1012-full-codebase-review.md`
**Target:** Fix all CRITICAL + HIGH + MEDIUM issues with logical ordering

## Dependencies

```
Phase 01 (C1 config validation) → Phase 02 (C4 token header)
Phase 01 → Phase 03 (H1 PII redaction)
Phase 02 (C4 token header) → Phase 05 (C3 race condition) [both touch auth flow]
Phase 04 (H5 typed state) → Phase 07 (C2 split SalesStateHandler)
Phase 05 (C3 race condition) → Phase 06 (M1 dedup tenant query)
Phase 07 (C2 split) → Phase 08 (H2/H3 channel + Task.Run)
Phase 01-08 → Phase 09 (M3/M4 admin) → Phase 10 (M2 split files + low issues)
```

## Phase Status

| # | Phase | Status | Effort |
|---|-------|--------|--------|
| 01 | C1: Config validation restore | [ ] | 30min |
| 02 | C4: Move token to Bearer header | [ ] | 30min |
| 03 | H1: PII log redaction | [ ] | 1h |
| 04 | H5: Typed StateContext | [ ] | 1h |
| 05 | C3: Race condition fix | [ ] | 1h |
| 06 | M1: Deduplicate tenant query | [ ] | 30min |
| 07 | C2: Extract draft order service | [ ] | 3h |
| 08 | H2/H3: Channel logging + concurrency limit | [ ] | 1h |
| 09 | H4: Prompt injection guardrails | [ ] | 30min |
| 10 | M3: Order TenantId + M4: Admin pagination | [ ] | 1h |
| 11 | M2: Split large files + LOW issues | [ ] | 2h |

## Risk Summary

| Phase | Risk | Mitigation |
|-------|------|-----------|
| C2 split | Breaking changes in handler wiring | Extensive tests before/after |
| C3 race | Live site disruption during fix | Idempotent SQL with `ON CONFLICT` |
| C4 token header | Facebook API may still accept query string | Test both paths, keep fallback |
| H5 typed state | JSON deserialization break on existing data | Migration fallback: parse old dict |
| M2 split | High file count, easy to introduce bugs | Split only top 8 offenders, not all 18 |

## Rollback Strategy

Each phase modifies distinct file sets. Rollback = revert specific phase commits. Phase 05 (C3) and Phase 10 (M3) require migration rollback scripts.

---

Detailed phases: `phase-01-config-validation.md` through `phase-11-split-large-files.md`
