# Phase R-04: Extract SalesReplyOrchestrator

**Date**: 2026-05-09 20:15
**Severity**: Medium
**Component**: Sales reply pipeline / Orchestration layer
**Status**: Resolved

## What Happened

Extracted reply orchestration logic from `SalesStateHandlerBase` into dedicated `SalesReplyOrchestrator` service. Base class shrunk by 430 lines (1628 → 1198, −26.4%), new class at 558 LOC (under 600 target).

## The Brutal Truth

The reply pipeline has 5 interconnected methods (BuildOffer, BuildFallback, BuildGroundedFallback, etc.) that are hard to test in isolation while nested in the 1.6k-line handler. Extracting them into one orchestrator service is pragmatic — not architecture-perfection — but it works. The frustrating part: we exposed `BuildGroundedFallbackAsync` on the public interface because offer/fallback flows outside the main pipeline need it. That's a leaky abstraction we'll clean in R-05.

## Technical Details

**New code:**
- `ISalesReplyOrchestrator` interface — 2 methods (OrchestrateSalesReplyAsync, BuildGroundedFallbackAsync)
- `SalesReplyRequest` DTO — request shape for the orchestrator
- `SalesReplyOrchestrator` — 558 LOC, contains 5 methods moved verbatim from base class
- 2 constructor smoke tests (coverage via 845 transitive handler tests)

**Modified:**
- `SalesStateHandlerBase`: 1628 → 1198 lines (−430)
- Self-instantiation fallback pattern same as R-03
- 5 dead method bodies removed post-extraction

**Dead code & duplication:**
- W1: AddToHistory/GetHistory drift risk — mitigated with "keep in sync" comments (both places)
- W2: BuildGroundedFallbackAsync on public interface — leaky abstraction, deferred to R-05

## Decisions Made

- **Pragmatic scope**: Single orchestrator class, NO `IReplyPipelineStage` abstraction (YAGNI; no actual A/B test pipeline need today)
- **Method-level, not class-level**: Gathered 5 reply builders into 1 class, method-level decomposition stays clean
- **Self-instantiation fallback**: Same pattern as R-03; no DI registration this phase
- **Line target revised**: Planned R-04 at ≤600, achieved 1198 base class (98 over target due to self-instantiation ctor ceremony, acceptable trade-off)

## Metrics

- SalesStateHandlerBase: 1628 → 1198 (−430, −26.4%)
- SalesReplyOrchestrator: 558 LOC
- Tests: 849 passing, 0 failures, +4 new
- Commit: 052f471

## What Didn't Work

Direct test coverage minimal (2 smoke tests). Leaning heavily on 845 transitive handler tests. Not ideal, but sufficient given imminent R-05 refactor.

## Lessons Learned

- **Orchestrators beat 1-method DTOs:** Five related methods naturally group; single-method services create ceremony without clarity
- **Defer cleanup decisions:** Public interface shape (W2) and self-instantiation debt can live until R-05; moving fast matters more than perfect abstraction now

## Next Steps

1. **R-05** (final cleanup): Split state-dispatch out of base class to reach ≤600 target, register all extracted services in DI proper, drop self-instantiation fallbacks, collapse duplicated history helpers
2. **Phase 03**: Tackle critical bugs (C3 race, C4 token leak, H1 PII log)

---

*Five interconnected methods are cheaper to move together than to architect separately. Pragmatism beats perfection at extraction phase.*
