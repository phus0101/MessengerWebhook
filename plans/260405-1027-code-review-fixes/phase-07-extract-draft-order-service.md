---
phase: 07
title: "C2: Split SalesStateHandlerBase + Extract Draft Order Service"
priority: P1 (Critical)
status: pending
depends_on: 04
---

## Overview
Split 786-line `SalesStateHandlerBase.cs` into focused components. Extract 4x duplicate draft order creation into a single `DraftOrderCoordinator` service.

## Files to Create
- `src/MessengerWebhook/Services/DraftOrderCoordinator.cs` (new, ~120 lines)
- `src/MessengerWebhook/Services/Sales/VipGreetingHandler.cs` (new, ~120 lines)
- `src/MessengerWebhook/Services/Sales/CtaBuilder.cs` (new, ~100 lines)
- `src/MessengerWebhook/Services/Sales/SalesConversationHandler.cs` (new, ~150 lines)

## Files to Modify
- `src/MessengerWebhook/Services/SalesStateHandlerBase.cs` (reduce to <200 lines, delegate to new services)
- `src/MessengerWebhook/Services/ConversationStateMachine.cs` (update handler registration if needed)

## Implementation Steps

1. **Create `DraftOrderCoordinator` service**
   - Extract all draft order creation logic from 4 duplicate locations
   - Single method: `CreateOrGetDraftOrderAsync(string customerId, string tenantId, CancellationToken ct)`
   - Handle race condition: if draft already exists for customer, return existing instead of creating new
   - Add idempotency key: `Customer:{customerId}:DraftOrder` to prevent duplicates
   - Register as scoped service in DI

2. **Extract `VipGreetingHandler`**
   - Move VIP customer greeting logic (personalized message, history lookup)
   - Handle VIP detection and greeting CTA construction

3. **Extract `CtaBuilder`**
   - Move call-to-action message construction logic
   - Handle product recommendation formatting
   - Handle order confirmation messages

4. **Extract `SalesConversationHandler`**
   - Move core conversation flow state handling
   - Handle state transitions: inquiry -> consideration -> purchase intent -> order

5. **Refactor `SalesStateHandlerBase`**
   - Reduce to <200 lines by delegating to injected services
   - Keep only: DI setup, state routing, error handling wrapper
   - Inject: `DraftOrderCoordinator`, `VipGreetingHandler`, `SalesConversationHandler`
   - Keep `CtaBuilder` as static/util class (no DI needed)

## Success Criteria
- `SalesStateHandlerBase.cs` under 200 lines
- No duplicate draft order creation code
- All new files under 200 lines each
- Existing tests pass (no behavior change)
- `dotnet build` succeeds

## Risk Assessment
- **Likelihood:** High - large refactoring, many moving parts
- **Impact:** High if state machine wiring breaks
- **Mitigation:** Run full test suite after each extraction step. Extract in order: DraftOrderCoordinator -> VipGreetingHandler -> CtaBuilder -> SalesConversationHandler -> refactor base

## Rollback
Revert the entire refactoring commit. Original file preserved in git history.
