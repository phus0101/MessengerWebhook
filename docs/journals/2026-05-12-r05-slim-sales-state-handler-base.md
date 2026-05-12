# Phase R-05: Slim SalesStateHandlerBase Final Cleanup

**Date**: 2026-05-12 14:30
**Severity**: Medium
**Component**: Sales state handler / Core orchestration
**Status**: Resolved

## What Happened

Final cleanup phase of R-series refactoring. Extracted 3 major concerns from `SalesStateHandlerBase` (1365 → 840 LOC, −38% this phase; −65% end-to-end R-01 through R-05):
- **ISalesConsultationReplies** + **SalesConsultationReplies** (333 LOC) — 9 consultation reply builders (offer, product, shipping, price, inventory, greeting, ambiguity, order-confirmation)
- **11 message predicates** — moved to `SalesMessageParser` static partial
- **ConversationHistoryHelper** — deduplicates `GetHistory`/`AddToHistory` across 3 handlers

## The Brutal Truth

The frustration: small-talk responses were being double-written to `conversationHistory`. Once in `SalesReplyOrchestrator.GenerateAsync`, again in `SalesStateHandlerBase:745`. Burned 2 hours tracking ghost history entries before review caught it. Single ownership rule should have been explicit from R-04. This is the kind of debt that multiplies fast in orchestration layers.

## Technical Details

**New code:**
- `ISalesConsultationReplies` interface — 9 builder methods
- `SalesConsultationReplies` service (333 LOC) — DI-registered
- `ConversationHistoryHelper` — static class, 3 shared methods

**Modified:**
- `SalesStateHandlerBase`: 1365 → 840 lines (−525, −38%)
- `SalesMessageParser`: 5 new static predicates added as partial methods
- Program.cs: 5 new DI registrations (ISalesContextResolver, ISalesPromptBuilder, IContactConfirmationFlow, ISalesReplyOrchestrator, ISalesConsultationReplies)

**Design decision:**
- `??` fallback pattern preserved — `new SalesConsultationReplies(..., NullLogger<SalesConsultationReplies>.Instance)` — maintains backward compat with 849 unit tests that directly instantiate handlers

## Bugs Fixed

**W1 (Critical):** Small-talk history duplication
- Root: `SalesReplyOrchestrator:XXX` was writing assistant turn to history
- Same write happened again in `SalesStateHandlerBase:745`
- Fix: Removed internal write in orchestrator; `SalesStateHandlerBase:745` now owns assistant-turn history writes
- Impact: Duplicate entries were corrupting conversation context; now clean

## Metrics

- SalesStateHandlerBase: 1365 → 840 lines (−525, −38%)
- SalesConsultationReplies: 333 LOC
- Deduplication savings: −~60 LOC across 3 handlers
- Tests: 849/849 unit tests passing, 0 failures
- Build: 0 compile errors
- Commit: e50abc9

## Lessons Learned

- **Ownership explicit, not implicit:** History writes should be annotated with `// Single owner: SalesStateHandlerBase:745`
- **Double-check orchestrator boundaries:** When extracting methods that mutate shared state, trace ALL write paths
- **Conversation history is a bottleneck:** Multiple handlers writing same field = hidden dependencies; consider event-sourced audit trail next phase

## Next Steps

1. **Phase 03**: Tackle critical bugs (C3 race condition, C4 token leak, H1 PII logging)
2. **Refactor assessment**: Base class now 840 LOC; next major cleanup = state dispatch extraction (~Phase 06 backlog)
3. **Consider:** Conversation history event sourcing to eliminate multi-owner writes

---

*Final extraction complete. The real lesson wasn't code size — it was finding the ghost write that was corrupting state.*
