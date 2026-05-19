# Phase 5: Typed Conversation State Model (H5)

## Overview
- Priority: High
- Current status: Not started (depends on Phase 1 — DraftOrderCoordinator extraction)
- Effort: 1.5h
- Issue: H5 — Dictionary-based StateContext has no type safety

## Problem
`StateContext.Data` is `Dictionary<string, object>` — typos in keys silently create orphaned data. No compile-time safety for required fields vs optional.

Current keys used across codebase:
- `customerPhone` (string)
- `shippingAddress` (string)
- `selectedProductCodes` (List<string>)
- `selectedGiftCode` / `selectedGiftName` (string)
- `draftOrderId` / `draftOrderCode` (string)
- `conversationHistory` (List<AiConversationMessage>)
- `rememberedCustomerPhone` / `rememberedShippingAddress` (string)
- `contactNeedsConfirmation` (bool)
- `contactMemorySource` (string)
- `consultationRejectionCount` (int)
- `consultationDeclined` (bool)
- `vipGreetingSent` (bool)
- `facebookPageId` (string)
- `shippingFee` (decimal)
- `supportCaseId` (Guid)
- `rememberedCustomerAddresses` (multiple — inconsistency)

## Context Links
- `src/MessengerWebhook/StateMachine/Models/StateContext.cs`
- All state handlers in `src/MessengerWebhook/StateMachine/Handlers/`

## Architecture

Add typed data model alongside the dictionary (backward compatible migration):

```csharp
public class SalesSessionData
{
    public string? CustomerPhone { get; set; }
    public string? ShippingAddress { get; set; }
    public List<string> SelectedProductCodes { get; set; } = new();
    public string? SelectedGiftCode { get; set; }
    public string? SelectedGiftName { get; set; }
    public decimal ShippingFee { get; set; }
    public string? DraftOrderId { get; set; }
    public string? DraftOrderCode { get; set; }
    public bool ContactNeedsConfirmation { get; set; }
    public string? ContactMemorySource { get; set; }
    public string? RememberedCustomerPhone { get; set; }
    public string? RememberedShippingAddress { get; set; }
    public int ConsultationRejectionCount { get; set; }
    public bool ConsultationDeclined { get; set; }
    public bool VipGreetingSent { get; set; }
    public string? FacebookPageId { get; set; }
    public string? SupportCaseId { get; set; }
    public List<AiConversationMessage> ConversationHistory { get; set; } = new();
}
```

Add strongly-typed accessors to StateContext:
```csharp
public SalesSessionData Sales { get; } = new();

// Keep GetData/SetData for backward compatibility during migration
```

## Implementation Steps

### Step 1: Create SalesSessionData.cs

Create `src/MessengerWebhook/StateMachine/Models/SalesSessionData.cs` with all known keys as typed properties.

### Step 2: Add typed accessors to StateContext.cs

Add `public SalesSessionData Sales { get; } = new();` property to StateContext.

### Step 3: Update call sites incrementally

In `SalesStateHandlerBase.cs` (after Phase 1 refactoring), `SalesMessageParser.cs`, and all other state handlers, replace:
```csharp
// Before
ctx.SetData("customerPhone", phone);
var phone = ctx.GetData<string>("customerPhone");

// After
ctx.Sales.CustomerPhone = phone;
var phone = ctx.Sales.CustomerPhone;
```

### Step 4: Keep GetData/SetData as fallback

Don't remove the dictionary — it provides fallback for keys not yet migrated. The typed model is additive.

## Related Code Files

**To create:**
- `src/MessengerWebhook/StateMachine/Models/SalesSessionData.cs`

**To modify:**
- `src/MessengerWebhook/StateMachine/Models/StateContext.cs` (add Sales property)
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (update accessors)
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs` (update accessors)
- All other state handlers that access StateContext.Data

## Todo List

- [ ] Create SalesSessionData.cs with all known properties
- [ ] Add Sales property to StateContext
- [ ] Update SalesStateHandlerBase.cs accessors
- [ ] Update SalesMessageParser.cs accessors
- [ ] Update remaining state handlers
- [ ] Run dotnet build
- [ ] Run state machine unit tests

## Success Criteria

- All known state keys accessible via typed properties
- No remaining `ctx.GetData<string>("magicStringKey")` calls for migrated keys
- Dictionary still available as fallback for non-migrated keys
- `dotnet build` succeeds with 0 errors
- All state machine tests pass

## Risk Assessment

**Medium risk.** Many sites access the dictionary. Mitigation:
- Keep GetData/SetData working — only migrate to typed access incrementally
- The typed model is additive — doesn't break existing code
- Test suite covers state machine behavior — should catch any regression
