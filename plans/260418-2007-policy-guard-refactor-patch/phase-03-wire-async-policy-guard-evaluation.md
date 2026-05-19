# Phase 03 — Wire async policy guard evaluation

## Overview
Priority: P1  
Status: completed  
Goal: feed richer request context into the new guard without broad handler churn.

## Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesHandlerFallbacks.cs`
- `src/MessengerWebhook/Program.cs`

## Requirements
- Limit behavioral change to the escalation call point.
- Avoid touching every state handler if only base handler owns the policy check.
- Keep fallback constructor path usable in tests.

## Implementation steps
1. Register new collaborators in DI.
2. Keep `IPolicyGuardService` scoped and injectable as today.
3. Update `SalesStateHandlerBase` to build `PolicyGuardRequest` with recent history/state data.
4. Await `EvaluateAsync(...)` at the current policy gate.
5. Keep downstream escalation flow unchanged.
6. Update `SalesHandlerFallbacks` to construct `PolicyGuardService` with sensible defaults.

## Success criteria
- `SalesStateHandlerBase` compiles and still escalates through existing support-case flow.
- Tests using fallback services remain constructible.
- No unrelated handler logic changes.
