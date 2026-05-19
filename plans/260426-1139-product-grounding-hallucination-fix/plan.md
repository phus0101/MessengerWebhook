# Product Grounding Hallucination Fix Plan

## Status

- Phase 1: Completed and validated on 2026-04-26
- Phase 2: Deferred
- Phase 3: Deferred

## Scope

Implemented phase 1 from `docs/superpowers/specs/2026-04-26-product-grounding-hallucination-fix-design.md`.

Goal: stop Gemini from leaking product names that are not verified by DB/RAG/active product context, without DB migration.

## Phases

1. [Phase 1 - Product grounding gate](phase-01-product-grounding-gate.md) — completed

## Key dependencies

- `SalesStateHandlerBase` natural reply flow
- `RAGService` and `ContextAssembler`
- `ResponseValidationService`
- Unit test project

## Success criteria

- `mặt nạ dưỡng ẩm` requires product grounding. Done.
- Unknown product-like names in generated responses are blocked. Done.
- Verified active/RAG product names are allowed. Done.
- Hallucinated assistant history is not sent back to Gemini. Done.
- `dotnet build` passes. Done.
- Relevant unit tests pass. Done.

## Validation

- `dotnet build`: passed with 0 warnings and 0 errors.
- Targeted unit tests: passed 77/77.
- Full unit tests: passed 610/610.
- Independent tester: passed build, targeted tests, and full unit suite.
- Code review: no blocker for the Phase 1 incident class.

## Follow-up considerations

- Short lowercase or code-only product mentions may need extra Phase 2 hardening if they are valid customer-facing identifiers.
- `ProductGroundingService` and detectors can be moved to DI in a cleanup pass.
