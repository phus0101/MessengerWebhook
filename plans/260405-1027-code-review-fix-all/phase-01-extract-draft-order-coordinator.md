# Phase 1: Extract DraftOrderCoordinator Service (C2)

## Overview
- Priority: Critical
- Current status: Not started
- Effort: 2h
- Issue: C2 — SalesStateHandlerBase.cs is 786 lines with 4x duplicate draft order creation logic

## Problem
`SalesStateHandlerBase.cs` (786 lines) contains draft order creation duplicated at lines ~218, ~260, ~298, and a variant. This causes:
- Race condition: rapid messages create duplicate draft orders
- Maintenance burden: fix in 1 place, miss 3 others
- Inconsistent behavior between copies

## Context Links
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (786 lines — primary offender)
- `src/MessengerWebhook/Services/DraftOrders/IDraftOrderService.cs`
- `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`

## Architecture

Extract to `DraftOrderCoordinator` service that:
1. Checks if a draft order already exists (via `draftOrderId` in state)
2. Idempotently creates or returns existing draft
3. Returns consistent response format

```
SalesStateHandlerBase
  └── DraftOrderCoordinator (new service)
        ├── TryEnsureDraftOrderAsync(ctx)
        │     ├── Check: already has draftOrderId in ctx? → return existing
        │     ├── Check: bot lock active? → prevent race
        │     └── Create new via DraftOrderService.CreateFromContextAsync
        └── BuildConfirmationMessage(draft)
```

The 4 duplicate sites all become:
```csharp
var draft = await _draftOrderCoordinator.TryEnsureDraftOrderAsync(ctx);
ctx.CurrentState = ConversationState.Complete;
return draft.ConfirmationMessage;
```

Split remaining handlers into separate files:
- `VipGreetingHandler.cs` — VIP-specific logic
- `CtaContextBuilder.cs` — CTA building (BuildCtaContext, BuildVipInstruction)
- `SalesConversationHandler.cs` — main routing (HandleSalesConversationAsync)

## Implementation Steps

### Step 1: Create DraftOrderCoordinator.cs

Create `src/MessengerWebhook/Services/DraftOrders/DraftOrderCoordinator.cs`:
- Constructor: `IDraftOrderService draftOrderService`, `ILogger`
- `TryEnsureDraftOrderAsync(StateContext ctx, CancellationToken ct)`:
  1. If `ctx.GetData<string>("draftOrderId")` is set, return existing (idempotent guard)
  2. Call `DraftOrderService.CreateFromContextAsync(ctx)`
  3. Set `draftOrderId` and `draftOrderCode` in context
  4. Return draft

### Step 2: Create CtaContextBuilder.cs

Extract `BuildCtaContext`, `BuildVipInstruction`, `GetMissingContactInfo`, `GetContactSummary`, `BuildDraftConfirmation` into `src/MessengerWebhook/StateMachine/Handlers/CtaContextBuilder.cs` as static methods.

### Step 3: Create VipProfileHandler.cs

Extract `GetVipProfileAsync`, `BuildVipInstruction` into `src/MessengerWebhook/StateMachine/Handlers/VipProfileHandler.cs`.

### Step 4: Refactor SalesStateHandlerBase.cs

Replace 4 duplicate draft creation blocks with calls to `DraftOrderCoordinator.TryEnsureDraftOrderAsync`.
Delete extracted methods (they're now in new files).
Result should be under 200 lines.

### Step 5: Register DraftOrderCoordinator in Program.cs

```csharp
builder.Services.AddScoped<DraftOrderCoordinator>();
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Services/DraftOrders/DraftOrderCoordinator.cs`
- `src/MessengerWebhook/StateMachine/Handlers/CtaContextBuilder.cs`
- `src/MessengerWebhook/StateMachine/Handlers/VipProfileHandler.cs`

**To modify:**
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (extract logic, replace duplicates)
- `src/MessengerWebhook/Program.cs` (register DraftOrderCoordinator)

## Todo List

- [ ] Create DraftOrderCoordinator.cs with idempotent draft creation
- [ ] Create CtaContextBuilder.cs
- [ ] Create VipProfileHandler.cs
- [ ] Refactor SalesStateHandlerBase — replace 4 duplicates with coordinator calls
- [ ] Register DraftOrderCoordinator in DI
- [ ] Run dotnet build — verify no compile errors
- [ ] Run existing unit tests for SalesStateHandlerBase

## Success Criteria

- `SalesStateHandlerBase.cs` under 200 lines
- No duplicate draft order creation logic (all via coordinator)
- All existing unit tests pass
- `dotnet build` succeeds with 0 errors

## Risk Assessment

**Medium risk.** Large refactor touching core sales flow. Mitigation:
- Preserve exact behavior 1:1 — no behavioral changes, just extraction
- Run full test suite before and after
- Test both normal flow (contact + product) and edge cases (no product, consultation rejection)
