# Phase 01 — Introduce policy guard DTOs and options

## Overview
Priority: P1  
Status: completed  
Goal: add the data contracts and config surface needed for a layered guard without changing runtime behavior yet.

## Files
### Create
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `src/MessengerWebhook/Services/Policy/PolicyGuardRequest.cs`
- `src/MessengerWebhook/Services/Policy/PolicyConversationTurn.cs`
- `src/MessengerWebhook/Services/Policy/PolicySignal.cs`
- `src/MessengerWebhook/Services/Policy/PolicyAction.cs`
- `src/MessengerWebhook/Services/Policy/PolicyScoreResult.cs`
- `src/MessengerWebhook/Services/Policy/PolicyClassificationResult.cs`

### Modify
- `src/MessengerWebhook/Services/Policy/PolicyDecision.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyGuardService.cs`
- `src/MessengerWebhook/Configuration/SalesBotOptions.cs`

## Requirements
- Keep current guard call sites compiling.
- New DTOs must be policy-specific, not sales-state-specific.
- `PolicyDecision` additions must use defaults so old constructor usage remains valid or easy to update.

## Implementation steps
1. Create `PolicyAction` enum and keep it detached from support-case reason.
2. Create immutable request/turn DTOs for async evaluation.
3. Create `PolicySignal` and `PolicyScoreResult` for internal and audit use.
4. Extend `PolicyDecision` with optional metadata fields.
5. Add `EvaluateAsync(...)` to `IPolicyGuardService` while keeping `Evaluate(string)`.
6. Add `PolicyGuardOptions` for thresholds, detector flags, and boosts.
7. Leave existing CTA config in `SalesBotOptions` untouched.

## Success criteria
- Solution builds after DTO/interface additions.
- No behavior change yet.
- Existing handler code can still compile against compatibility methods.
