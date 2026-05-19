---
phase: 04
title: "H5: Typed StateContext Model"
priority: P2 (High)
status: pending
depends_on: none
---

## Overview
Replace `Dictionary<string, object>` StateContext with a typed record class for compile-time safety.

## Files to Modify
- `src/MessengerWebhook/Models/StateContext.cs` (replace dictionary with typed class)
- `src/MessengerWebhook/Models/StateContextKeys.cs` (new, const-based fallback)
- `src/MessengerWebhook/Services/SalesStateHandlerBase.cs` (update all dictionary accesses)
- `src/MessengerWebhook/Services/ConversationStateMachine.cs` (update state access)

## Implementation Steps

1. **Create typed StateContext record**
   - File: `src/MessengerWebhook/Models/StateContextData.cs` (new)
   ```csharp
   public record StateContextData
   {
       public string? CustomerPhone { get; init; }
       public string? CustomerAddress { get; init; }
       public string? CustomerName { get; init; }
       public long? DraftOrderId { get; init; }
       public string? LastProductId { get; init; }
       public int? MessageCount { get; init; }
       public DateTime? LastInteractionAt { get; init; }
       public Dictionary<string, object> AdditionalData { get; init; } = new();
   }
   ```

2. **Update StateContext class**
   - Replace `Dictionary<string, object> Data` with `StateContextData Data`
   - Add migration path: deserialize old JSON format, convert dict to typed properties
   - Keep `AdditionalData` dict for extensibility (unknown/future keys)

3. **Update all consumers**
   - Search pattern: `stateContext.Data["` and `.TryGetValue(`
   - Replace with direct property access: `stateContext.Data.CustomerPhone`
   - All 5-7 locations across SalesStateHandlerBase and ConversationStateMachine

4. **Update serialization**
   - JSON serializer will now produce typed output, update any hardcoded JSON in tests
   - Add migration test: deserialize old dict format, verify conversion works

## Success Criteria
- Zero dictionary string keys for known state properties
- Compile-time errors on typos (previously runtime silent failures)
- Old serialized state data still deserializes correctly
- `dotnet build` succeeds, tests pass

## Risk Assessment
- **Likelihood:** Medium - breaking change to serialization format
- **Impact:** Medium if existing state data fails to deserialize
- **Mitigation:** Migration fallback in deserializer, test with sample old data

## Rollback
Revert commit. Dictionary-based serialization is backward compatible.
