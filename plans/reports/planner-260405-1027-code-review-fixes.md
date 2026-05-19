# Planner Report: Code Review Fixes

**Date:** 2026-04-05
**Source:** `docs/code-review-260405-1012-full-codebase-review.md`
**Plan:** `plans/260405-1027-code-review-fixes/`

## Plan Created

11 phases covering all CRITICAL, HIGH, MEDIUM, and LOW issues:

| Task ID | Phase | Issue | Effort | Depends |
|---------|-------|-------|--------|---------|
| 22 | P01: Config validation | C1 | 30min | — |
| 31 | P02: Token → Bearer header | C4 | 30min | — |
| 20 | P03: PII log redaction | H1 | 1h | — |
| 33 | P04: Typed StateContext | H5 | 1h | — |
| 19 | P05: Race condition fix | C3 | 1h | — |
| 23 | P06: Dedup tenant query | M1 | 30min | P05 |
| 21 | P07: Split SalesStateHandler | C2 | 3h | P04 |
| 26 | P08: Channel + concurrency | H2/H3 | 1h | P07 |
| 25 | P09: Prompt guardrails | H4 | 30min | — |
| 29 | P10: Order TenantId + pagination | M3/M4 | 1h | P05 |
| 27 | P11: Split files + test fixes | M2/LOW | 2h | P07 |

**Total estimated effort:** ~12.5h

## Dependency Graph

```
P01 (C1) ──────────────────┐
P02 (C4) ──────────────────┤
P03 (H1) ──────────────────┤
P09 (H4) ──────────────────┤ → independent, parallelizable
P04 (H5) ───────┐          │
P05 (C3) ───────┤──────┬───┤
  P06 (M1) ← P05      │   │
  P10 (M3/M4) ← P05   │   │
  P07 (C2) ← P04 ─────┼───┘
    P08 (H2/H3) ← P07 ──┘
    P11 (M2/LOW) ← P07 ─┘
```

**Execution order (parallel groups):**
- Group 0: P01, P02, P03, P04, P05, P09 (6 tasks, all independent)
- Group 1: P06 (blocked by P05), P07 (blocked by P04), P10 (blocked by P05)
- Group 2: P08 (blocked by P07), P11 (blocked by P07)

## Key Decisions

1. **P07 (C2) scope limited to top offender only** - SalesStateHandlerBase (786 lines) extracted. Other large files handled in P11 but only top 4-5 split, not all 18 (YAGNI).
2. **P05 uses ON CONFLICT DO NOTHING** instead of Redis distributed lock - simpler, no Redis dependency for single-instance deployment.
3. **P10 migration handles orphaned orders** - existing orders without sessions must get TenantId from somewhere or get soft-deleted.
4. **L4 (Vietnamese strings) deferred** - .resx localization for single-language app is over-engineering.
5. **L5 (ModelSnapshot 1,571 lines) deferred** - generated file, note for future migration squash.

## Unresolved Questions

1. **Multi vs Single instance deployment?** - Affects whether Channel<T> + IMemoryCache need Redis replacement. Plan assumes single instance.
2. **Channel DropOldest business impact?** - Is silent message loss acceptable for sales messages? Plan adds logging but keeps DropOldest mode.
3. **Are there actual duplicate FacebookPageConfigs in production?** - Migration adding unique index may fail if duplicates exist. Pre-migration cleanup needed.
