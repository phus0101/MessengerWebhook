---
title: "Phase 03 - Handler integration and validation"
description: "Wire DB-grounded suggestions into fallback branches while preserving selection semantics."
status: completed
priority: P1
effort: 2h
branch: master
tags: [sales-state-handler, response-validation, fallback]
created: 2026-04-26
---

# Phase 03 - Handler Integration and Validation

## Context Links
- Overview: `plan.md`
- Depends on: `phase-02-grounding-service-related-suggestions.md`
- File: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

## Overview
Replace current immediate safe fallback at both grounding failure points with: try DB-grounded related suggestions; if none, existing fallback. Do not edit unrelated conversation flow.

## Current Fallback Points
- Treatment path around `groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts`.
- Control/direct path around same condition.

## Requirements
### Functional
- Both treatment and control/direct paths use same suggestion logic.
- Related suggestion response is returned directly; Gemini must not rewrite it.
- Validate suggestion response with `ResponseValidationService` using suggested products as allowed facts.
- If validation fails, return existing `FallbackReply` and log warning.
- Do not mutate selected product context until customer chooses.

### Non-functional
- Minimal code churn in large handler.
- Shared helper method in same class to avoid duplication between paths.
- Maintain backward compatibility for existing A/B test paths.

## Architecture / Data Flow
Input: `StateContext ctx`, message, `GroundedProductContext` with no allowed products.
Transform:
1. Get tenant id from existing context source used by active product retrieval/RAG.
2. Ask grounding service for related suggestions or suggestion reply.
3. If no suggestions: return `FallbackReply`.
4. Validate deterministic suggestion reply:
   - allowedProducts = related suggestions
   - `requiresProductGrounding = true`
   - `allowPolicyFacts = false`
   - `allowInventoryFacts = false`
   - `allowOrderFacts = false`
5. On valid: add assistant history if existing pattern does so for direct returns; otherwise return consistently with current fallback behavior.
Exit: safe suggestion reply or safe fallback.

## Related Code Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Possibly constructor dependency injection setup in `Program.cs` only if Phase 02 changes `ProductGroundingService` from manual `new` to DI.

### Create
- None.

### Delete
- None.

## Implementation Steps
1. Inject or otherwise access product repository-backed grounding suggestion capability.
2. Add private helper, e.g. `TryBuildGroundedRelatedSuggestionAsync(...)`, owned by this handler.
3. Replace treatment fallback block:
   - try suggestion
   - return suggestion if valid
   - else existing fallback
4. Replace control/direct fallback block identically.
5. Ensure suggestions are not stored in `selectedProductCodes`, `selectedProducts`, draft order data, or selected product context.
6. Preserve RAG and existing allowed-product flow when allowed products exist.

## Todo List
- [x] Identify tenant id source and fail closed if missing.
- [x] Add shared fallback helper.
- [x] Wire treatment path.
- [x] Wire control/direct path.
- [x] Add validation fail-closed path.
- [x] Confirm selected product context unchanged.

## Validation Results
- `SalesStateHandlerBase` uses the repository-backed grounding service in treatment, control/direct, and RAG-disabled fallback paths.
- Related suggestion replies bypass Gemini rewriting, pass through `ResponseValidationService`, and fail closed to `ProductGroundingService.FallbackReply` if validation rejects them.
- Handler tests assert both A/B branches return grounded suggestions, validation failure returns fallback, Gemini is not called for suggestion rewriting, and `selectedProductCodes` is unchanged.
- `dotnet test "tests/MessengerWebhook.UnitTests" --filter "ProductGrounding|ProductRepository|SalesStateHandlerBase" -p:UseAppHost=false`: passed 55/55.
- `dotnet test "tests/MessengerWebhook.UnitTests" -p:UseAppHost=false`: passed 620/620.
- `dotnet build -p:UseAppHost=false`: passed with 0 warnings and 0 errors.

## Success Criteria
- Current exact fallback still reachable when no suggestions.
- Suggestion response bypasses Gemini generation.
- Response validation blocks unexpected product/policy/inventory claims.
- Both branches behave consistently.
- No unrelated handler behavior changed.

## Risk Assessment
| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Handler constructor churn breaks tests | Medium | Medium | Prefer minimal DI change; update test builders in Phase 04 |
| Missing tenant id causes broad query | Low | High | Fail closed to fallback; repository requires tenant id |
| Duplicate branch behavior diverges | Medium | Medium | One helper used by both fallback points |
| Validation rejects safe suggestions | Medium | Medium | Align allowed products with suggestions; fallback if invalid |

## Security Considerations
- Tenant id missing/invalid = safe fallback.
- No user-specific private data in suggestion response.
- No unvalidated policy/stock/effect claims.

## Test Matrix
- Unit: treatment branch vague request + related products returns suggestions.
- Unit: control/direct branch same behavior.
- Unit: validation failure returns existing fallback.
- Unit: missing tenant id returns existing fallback.
- Unit: `selectedProductCodes` unchanged after suggestions.

## Rollback Plan
Revert the two fallback branch changes or feature-toggle helper to always return null. Existing fallback remains intact and can be restored without DB/data changes.

## Next Steps
Phase 04 adds/updates tests and docs based on final implementation shape.
