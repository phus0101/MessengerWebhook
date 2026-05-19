---
title: "Phase 04 - Tests and docs sync"
description: "Validate related-products fallback end-to-end and update docs only if behavior changes require it."
status: completed
priority: P1
effort: 2h
branch: master
tags: [tests, docs, validation]
created: 2026-04-26
---

# Phase 04 - Tests and Docs Sync

## Context Links
- Overview: `plan.md`
- Depends on: `phase-03-handler-integration-and-validation.md`
- Existing tests: `tests/MessengerWebhook.UnitTests/Services/ProductGrounding/ProductGroundingServiceTests.cs`, `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

## Overview
Prove production behavior with focused unit tests and build. Update docs only if implementation changes customer-visible behavior docs or architecture docs.

## Requirements
### Functional Test Coverage
- Vague product need with related DB-confirmed products returns max 2-3 suggestions.
- No related products returns existing safe fallback.
- Inactive products excluded.
- Other-tenant products excluded.
- No product hallucination or ungrounded price/policy claim.
- Suggestions do not mutate selected product context.

### Non-functional Validation
- `dotnet build` passes.
- Relevant unit tests pass.
- No DB migration required unless explicitly justified.
- Docs synced if customer-visible fallback behavior is documented.

## Test Matrix
| Layer | Test | Expected |
|---|---|---|
| Unit - criteria | `mặt nạ dưỡng ẩm` maps to safe related criteria | criteria non-empty and deterministic |
| Unit - grounding service | DB product list builds suggestion reply | <=3 products, names/codes from list only |
| Unit - grounding service | no products | existing `FallbackReply` |
| Unit - repository | inactive product | excluded |
| Unit/repository | other tenant product | excluded |
| Unit - handler treatment | vague request + related products | deterministic suggestion returned, no Gemini rewrite |
| Unit - handler control/direct | vague request + related products | same result as treatment |
| Unit - handler | validation fails | existing safe fallback |
| Unit - state | suggestion returned | `selectedProductCodes` unchanged |
| Build | solution compile | 0 errors |

## Related Code Files
### Modify
- `tests/MessengerWebhook.UnitTests/Services/ProductGrounding/ProductGroundingServiceTests.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- Add repository tests in existing unit/integration test location if repository test patterns exist.
- `docs/project-changelog.md`, `docs/system-architecture.md`, or sales bot docs only if implementation phase changes documented behavior.

### Create
- Prefer existing test files. Create new test file only if existing files exceed maintainability limits or repository tests have established folder conventions.

### Delete
- None.

## Implementation Steps
1. Add service tests for criteria extraction and suggestion reply formatting.
2. Add repository tests for tenant/active filtering using existing test DB/in-memory pattern. Do not mock away tenant filtering if current project has real DB test pattern.
3. Add handler tests for both fallback points and state mutation semantics.
4. Add validation-fail test.
5. Run targeted tests:
   - `dotnet test tests/MessengerWebhook.UnitTests --filter ProductGrounding`
   - `dotnet test tests/MessengerWebhook.UnitTests --filter SalesStateHandlerBase`
6. Run `dotnet build`.
7. Update docs/changelog only if final code changes documented architecture or bot operating behavior.

## Todo List
- [x] Add product grounding service tests.
- [x] Add repository tenant/active filtering tests.
- [x] Add handler treatment/control tests.
- [x] Add selected context immutability test.
- [x] Run targeted tests.
- [x] Run build.
- [x] Decide docs impact and update docs if needed.

## Validation Results
- `dotnet test "tests/MessengerWebhook.UnitTests" --filter "ProductGrounding|ProductRepository|SalesStateHandlerBase" -p:UseAppHost=false`: passed 55/55.
- `dotnet test "tests/MessengerWebhook.UnitTests" -p:UseAppHost=false`: passed 620/620.
- `dotnet build -p:UseAppHost=false`: passed with 0 warnings and 0 errors.
- Docs impact: customer-visible fallback behavior changed, so changelog and architecture docs updated.

## Success Criteria
- Acceptance criteria from `plan.md` are test-covered.
- Build passes.
- Relevant tests pass.
- Docs impact explicitly stated by implementer.

## Risk Assessment
| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Tests over-mock repository and miss tenant bug | Medium | High | Prefer real EF query test pattern; assert tenant exclusion |
| Handler tests brittle due large constructor | Medium | Medium | Use existing `SalesStateHandlerBaseTests` builders/patterns |
| Docs drift | Low | Medium | Update only docs that already describe fallback/sales bot behavior |
| Full suite slow/flaky | Medium | Low | Run targeted tests + build minimum; full suite before push if implementation proceeds |

## Security Considerations
- Tests must assert cross-tenant exclusion explicitly.
- Tests must assert ungrounded product names are absent from response.

## Rollback Plan
Tests/docs can be reverted independently. If implementation rolls back, remove behavior-specific tests and docs changes in same rollback commit.

## Next Steps
After all tests/build pass, hand off to code-reviewer. Do not merge if tenant isolation or hallucination safety tests fail.
