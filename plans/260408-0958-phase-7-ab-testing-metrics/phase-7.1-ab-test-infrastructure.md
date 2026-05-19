---
phase: 7.1
title: "A/B Test Infrastructure"
effort: 4h
status: pending
dependencies: []
---

# Phase 7.1: A/B Test Infrastructure

## Context

Build deterministic A/B test assignment system to compare naturalness pipeline (treatment) vs baseline (control). Assignment must be consistent per PSID across all messages in a session.

**Related Files**:
- `docs/system-architecture.md` (lines 317-681) - Naturalness pipeline architecture
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - Pipeline integration point
- `src/MessengerWebhook/Data/Entities/ConversationSession.cs` - Session entity

## Overview

**Priority**: P1 (blocks Phase 7.2)  
**Status**: Pending  
**Effort**: 4 hours

## Key Insights

- Hash-based assignment ensures consistency (same PSID always gets same variant)
- Feature flag allows instant rollback without code deploy
- Control group skips entire pipeline → measures baseline performance
- Treatment group runs full pipeline → measures naturalness impact

## Requirements

### Functional
- Assign users to control/treatment based on PSID hash
- Store variant in `conversation_sessions.ab_test_variant`
- Assignment persists across session lifecycle
- Feature flag to enable/disable A/B test
- Configurable treatment percentage (default 50%)

### Non-Functional
- Assignment latency: <5ms (hash computation)
- Zero impact on existing sessions (backward compatible)
- Deterministic: same PSID → same variant always
- Thread-safe for concurrent requests

## Architecture

### Data Flow

```
User Message → ABTestService.GetVariantAsync(PSID)
    ↓
Check session.ABTestVariant (cached)
    ↓
If NULL: Hash(PSID + seed) % 100 → 0-49: Control, 50-99: Treatment
    ↓
Store in session.ABTestVariant
    ↓
Return variant to SalesStateHandlerBase
    ↓
If Control: Skip pipeline → Direct AI
If Treatment: Full pipeline → Emotion → Tone → Context → SmallTalk → Validation
```

### Hash Algorithm

```csharp
// Deterministic assignment using SHA256
public string AssignVariant(string psid, int treatmentPercentage)
{
    var input = $"{psid}:{_options.HashSeed}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    var bucket = BitConverter.ToUInt32(hash, 0) % 100;
    return bucket < treatmentPercentage ? "treatment" : "control";
}
```

**Why SHA256?**
- Cryptographically secure → uniform distribution
- Deterministic → same input always produces same output
- Fast → <1ms for hash computation

## Related Code Files

### Files to Create

**1. `Services/ABTesting/IABTestService.cs`**
**2. `Services/ABTesting/ABTestService.cs`**
**3. `Services/ABTesting/Configuration/ABTestingOptions.cs`**
**4. `Services/ABTesting/Configuration/ValidateABTestingOptions.cs`**

### Files to Modify

**1. `Data/Entities/ConversationSession.cs`** - Add `ABTestVariant` property
**2. `Data/MessengerBotDbContext.cs`** - Add index
**3. `StateMachine/Handlers/SalesStateHandlerBase.cs`** - Integrate A/B logic
**4. `Program.cs`** - Register service
**5. `appsettings.json`** - Add config section

## Implementation Steps

1. **Create ABTesting service structure** (30min)
2. **Database migration** (20min)
3. **Integrate into SalesStateHandlerBase** (45min)
4. **Configuration and DI** (15min)
5. **Compile and verify** (30min)
6. **Manual testing** (40min)

## Todo List

- [ ] Create `Services/ABTesting/IABTestService.cs`
- [ ] Create `Services/ABTesting/ABTestService.cs`
- [ ] Create `Services/ABTesting/Configuration/ABTestingOptions.cs`
- [ ] Create `Services/ABTesting/Configuration/ValidateABTestingOptions.cs`
- [ ] Add `ABTestVariant` property to `ConversationSession.cs`
- [ ] Add index in `MessengerBotDbContext.cs`
- [ ] Create migration `AddABTestVariant`
- [ ] Run migration: `dotnet ef database update`
- [ ] Add `IABTestService` to `SalesStateHandlerBase` constructor
- [ ] Modify `BuildNaturalReplyAsync` to check variant
- [ ] Implement `GenerateDirectAIResponseAsync` method
- [ ] Update all StateHandler subclasses constructors
- [ ] Add `ABTesting` config to appsettings.json
- [ ] Register service in Program.cs
- [ ] Run `dotnet build` and fix errors
- [ ] Manual test: control variant
- [ ] Manual test: treatment variant
- [ ] Manual test: variant persistence
- [ ] Manual test: feature flag toggle

## Success Criteria

**Technical**:
- A/B test service compiles without errors
- Migration applied successfully
- Variant assignment deterministic (same PSID → same variant)
- Assignment latency <5ms (measured in logs)
- Control group skips pipeline (verified in logs)
- Treatment group runs pipeline (verified in logs)
- Variant persists across messages in session

**Business**:
- 50/50 split achieved (verify with 100 test PSIDs)
- Feature flag works (disable → all users get treatment)
- No errors in production logs

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Hash distribution skewed | Low | High | Unit test with 10K PSIDs, verify chi-square test p>0.05 |
| Variant assignment race condition | Low | Medium | Use database transaction for session update |
| Control group worse UX | Medium | High | Monitor escalation rate, add kill switch |
| Migration breaks existing sessions | Low | High | Nullable column, backward compatible |
| Performance regression | Low | Medium | Hash computation <5ms, async DB write |

## Security Considerations

- PSID hashed with seed → prevents reverse engineering variant assignment
- No PII stored in variant assignment logic
- Tenant isolation maintained (variant per session, session per tenant)
- Feature flag allows instant disable without code deploy

## Rollback Plan

**Immediate Rollback** (no code deploy):
1. Set `ABTesting.Enabled = false` in appsettings.json
2. Restart app → all users get treatment (full pipeline)
3. Zero data loss, sessions continue normally

**Full Rollback** (code deploy):
1. Remove `IABTestService` from `SalesStateHandlerBase`
2. Remove variant check in `BuildNaturalReplyAsync`
3. Drop migration: `dotnet ef migrations remove`
4. Deploy

## Next Steps

After Phase 7.1 completion:
1. Verify A/B assignment working in production
2. Monitor logs for variant distribution (should be ~50/50)
3. Proceed to Phase 7.2: Metrics Collection
4. Begin 2-week A/B test period

## Unresolved Questions

1. Hash seed rotation: Should we rotate seed weekly? (Recommendation: No, consistency more important)
2. Multi-tenant variant isolation: Separate A/B test per tenant? (Recommendation: No, global test first)
3. Variant override for testing: Add admin API to force variant? (Recommendation: Yes, add in Phase 7.3)
