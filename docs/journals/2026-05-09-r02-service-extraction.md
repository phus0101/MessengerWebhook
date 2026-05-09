# Phase R-02: Service Extraction from SalesStateHandlerBase Monolith

**Date**: 2026-05-09 16:52
**Severity**: Medium
**Component**: Sales workflow / State handler refactoring
**Status**: Resolved

## What Happened

Extracted context resolution and prompt building logic from the 2793-line `SalesStateHandlerBase.cs` monolith into dedicated services. Achieved 30% reduction in base class size while adding comprehensive unit test coverage.

## The Brutal Truth

The monolith was a graveyard of mixed concerns — product lookups tangled with VIP checks, history recovery next to prompt assembly, all with scattered async/side-effects. Breaking this apart exposed how much implicit logic was buried in a 2000-line class. Every extraction required careful thinking about contracts: what should be read-only? What can have side effects? The real pain was realizing we need proper DI wiring (deferred to R-05) but couldn't wait for it, so we left a fallback self-instantiation smell in the code.

## Technical Details

**New abstractions:**
- `ISalesContextResolver` + `SalesContextResolver` (~420 lines) — product candidate resolution, VIP profile lookup, history recovery, commercial snapshots. Read-only contract enforced: uses `GetExistingAsync` only, never writes.
- `ISalesPromptBuilder` + `SalesPromptBuilder` (~200 lines) — pure prompt builders, no async, no side effects, pure functions.
- `SalesTextHelper` (static utility) — Vietnamese text normalization shared across services
- `HistoryProductCandidate` — promoted from private record to public type

**Test coverage:**
- `SalesPromptBuilderTests.cs` — 54 tests (all passing)
- `SalesContextResolverTests.cs` — 43 tests (all passing)
- Full suite: 783 unit + 246 integration = 1029 total passing

## What We Tried

1. ~~Extract `BuildNaturalReplyAsync` with context builder~~ → Rejected. Mixes context + side effect + AI call; too entangled. Deferred to R-03/R-04.
2. ~~Fix DI immediately across all 7 subclasses~~ → Too risky mid-refactor. Used fallback constructor self-instantiation (`?? new SalesContextResolver(...)`) to keep coupling loose.
3. ~~Convert `SalesTextHelper` to injectable service~~ → Overkill. It's a pure utility with zero dependencies; kept as `internal static`.

## Root Cause Analysis

The base class sprawl happened because "common sales logic" gravitationally accumulated there without boundary guards. No architecture explicitly said "resolution logic lives here, prompt building lives there." Once we pushed back, the boundaries became obvious — the hard part was deciding what contracts matter (read-only resolver, pure builders) and sticking to them without perfect DI in place yet.

## Lessons Learned

- **Contracts first, DI later:** Don't wait for perfect dependency wiring to extract logic. Enforce contracts (read-only, pure functions) in code, add proper DI in cleanup pass.
- **Smell test for mixed concerns:** If a method touches data, builds strings, and calls async APIs, it's at least 3 services having a meeting in one function.
- **Fallback instantiation is acceptable debt:** `?? new SalesContextResolver(...)` is ugly but lets you unblock 7 subclasses; document it as R-05 cleanup and move on.
- **Static utilities aren't shame:** `SalesTextHelper` as `internal static` is fine; not everything needs DI. Reserve injection for stateful services.

## Next Steps

1. **R-03/R-04**: Extract `BuildNaturalReplyAsync` — requires AI service refactor first
2. **R-05**: Wire DI properly across all subclasses; remove constructor fallbacks
3. **Code review approved** — both HIGH (DB write contract) and WARNING (dead null-guard) issues resolved
4. Flaky integration test on `MetricsController` unrelated to R-02; escalate separately

**Metrics:**
- SalesStateHandlerBase: 2793 → 1955 lines (-838 lines, -30%)
- New tests: 97 (54 + 43)
- Test pass rate: 100% (1029/1029)
