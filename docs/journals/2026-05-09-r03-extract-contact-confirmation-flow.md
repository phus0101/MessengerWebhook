# Phase R-03: Extract ContactConfirmationFlow

**Date**: 2026-05-09 18:42
**Severity**: Medium
**Component**: Sales workflow / Contact confirmation logic
**Status**: Resolved

## What Happened

Extracted contact confirmation decision logic from `SalesStateHandlerBase` into a dedicated `IContactConfirmationFlow` service. Shrunk base class by 95 lines (1955 → 1860) while adding 66 unit tests, all passing.

## The Brutal Truth

Contact confirmation requires dual-state validation (`contactNeedsConfirmation=true` AND `pendingContactQuestion="confirm_old_contact"`), but the original code used a brittle snapshot pattern. The real frustration: the DTO (`ContactConfirmationDecision`) that I added in R-02 turned out to be dead code — never threaded through the handler flow. Cut it immediately post-review rather than carrying YAGNI bloat.

## Technical Details

**New code:**
- `IContactConfirmationFlow` + `ContactConfirmationFlow` (~130 lines) — pure read-only logic, mutates `StateContext` only in-memory, no DB writes or Messenger sends
- **Invariant documented:** Dual-state check encoded as `IsAwaitingOldContactConfirmation` private static to prevent future bugs
- **Live vs snapshot:** Changed from local snapshot to live `ctx` reads — safe because nothing mutates the flag between captures

**Removed:**
- `ContactConfirmationDecision.cs` — YAGNI DTO, never used

**Modified:**
- Constructor uses self-instantiation fallback (`?? new ContactConfirmationFlow(...)`) to defer DI wiring to R-05

**Test coverage:**
- 66 new unit tests, all passing
- Total suite: 845 passing, 0 failures

## What We Tried

1. ~~Full DI wiring across 7 subclasses~~ → Deferred to R-05 cleanup pass
2. ~~Keep ContactConfirmationDecision DTO~~ → Rejected post-review. Dead code penalty > benefit

## Root Cause Analysis

The confirmation logic was embedded in a larger handler method without clear boundaries. The dual-state invariant was implicit in the original code — encoded in comments only, easy to break.

## Lessons Learned

- **Document invariants in code, not comments:** Dual-state conditions deserve explicit helper methods (`IsAwaitingOldContactConfirmation`)
- **Kill dead code immediately:** ContactConfirmationDecision DTO was a distraction; cut it once review flagged no usage
- **Live state beats snapshots:** When safe, read current state instead of stale local variables — easier to reason about

## Next Steps

1. **R-04**: Extract `SalesReplyOrchestrator` (~3 days)
2. **R-05**: Final cleanup, proper DI registration, base class ≤400 lines target
3. **Phase 03**: Tackle critical bugs (C3 race, C4 token leak, H1 PII log)

**Metrics:**
- SalesStateHandlerBase: 1955 → 1860 lines (−95 lines, −4.9%)
- ContactConfirmationFlow: ~130 lines
- Tests: 845 total passing, 66 new
- Commit: 1bdb648
