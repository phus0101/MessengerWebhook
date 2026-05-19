---
phase: 7.2
title: "Metrics Collection Service"
effort: 5h
status: pending
dependencies: [7.1]
---

# Phase 7.2: Metrics Collection Service

## Context

Build metrics collection system to track naturalness pipeline performance and conversation outcomes. Metrics enable A/B test analysis and ongoing monitoring.

**Related Files**:
- Phase 7.1: A/B test infrastructure (provides variant assignment)
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - Integration point
- `docs/system-architecture.md` (lines 317-681) - Pipeline architecture

## Overview

**Priority**: P1 (blocks Phase 7.3)  
**Status**: Pending  
**Effort**: 5 hours  
**Dependencies**: Phase 7.1 (needs A/B variant)

## Key Insights

- Async logging prevents performance impact on user-facing requests
- JSONB storage provides flexibility for evolving metrics schema
- Batch writes reduce database load (100 metrics buffered, flush every 60s)
- Metrics tied to session_id enable conversation-level analysis

## Requirements

### Functional
- Log metrics for every message processed (control + treatment)
- Track emotion, tone, journey stage, validation results (treatment only)
- Track pipeline latency, total response time (both variants)
- Track conversation outcomes: completed, abandoned, escalated
- Store metrics in `conversation_metrics` table with JSONB flexibility

### Non-Functional
- Logging latency: <10ms (async, non-blocking)
- Zero impact on user response time
- Batch writes: 100 metrics per batch, 60s flush interval
- Memory usage: <50MB for in-memory buffer
- Tenant isolation: TenantId in all metrics

## Architecture

### Data Flow

```
Message Processed → Extract Metrics
    ↓
ConversationMetricsService.LogAsync()
    ↓
Add to in-memory buffer (ConcurrentQueue)
    ↓
Background flush every 60s OR buffer reaches 100 items
    ↓
Batch insert to conversation_metrics table
    ↓
Clear buffer
```

## Related Code Files

### Files to Create

1. `Services/Metrics/IConversationMetricsService.cs`
2. `Services/Metrics/ConversationMetricsService.cs`
3. `Services/Metrics/Models/ConversationMetricData.cs`
4. `Services/Metrics/Configuration/MetricsOptions.cs`
5. `Services/Metrics/MetricsBackgroundService.cs`
6. `Data/Entities/ConversationMetric.cs`

### Files to Modify

1. `StateMachine/Handlers/SalesStateHandlerBase.cs` - Add metrics logging
2. `Data/MessengerBotDbContext.cs` - Add ConversationMetrics DbSet
3. `Program.cs` - Register services
4. `appsettings.json` - Add Metrics config

## Implementation Steps

1. **Create Metrics service structure** (45min)
2. **Database migration** (30min)
3. **Background flush service** (30min)
4. **Integrate into SalesStateHandlerBase** (90min)
5. **Configuration and DI** (15min)
6. **Compile and verify** (30min)
7. **Manual testing** (60min)

## Todo List

- [ ] Create `Services/Metrics/IConversationMetricsService.cs`
- [ ] Create `Services/Metrics/ConversationMetricsService.cs`
- [ ] Create `Services/Metrics/Models/ConversationMetricData.cs`
- [ ] Create `Services/Metrics/Configuration/MetricsOptions.cs`
- [ ] Create `Services/Metrics/MetricsBackgroundService.cs`
- [ ] Create `Data/Entities/ConversationMetric.cs`
- [ ] Add `ConversationMetrics` DbSet to `MessengerBotDbContext.cs`
- [ ] Configure entity in `OnModelCreating`
- [ ] Create migration `AddConversationMetrics`
- [ ] Run migration: `dotnet ef database update`
- [ ] Add `IConversationMetricsService` to `SalesStateHandlerBase` constructor
- [ ] Capture metrics in `BuildNaturalReplyAsync`
- [ ] Update all StateHandler subclasses constructors
- [ ] Add `Metrics` config to appsettings.json
- [ ] Register services in Program.cs
- [ ] Run `dotnet build` and fix errors
- [ ] Manual test: control metrics
- [ ] Manual test: treatment metrics
- [ ] Manual test: batch flush
- [ ] Manual test: database records
- [ ] Manual test: feature flag toggle

## Success Criteria

**Technical**:
- Metrics service compiles without errors
- Migration applied successfully
- Metrics logged for 100% of messages
- Batch flush working (60s interval or 100 items)
- Async logging <10ms overhead
- No blocking on user-facing requests
- Tenant isolation maintained

**Business**:
- Metrics visible in database for both variants
- Control metrics have NULL pipeline fields
- Treatment metrics have full pipeline data
- Performance metrics captured accurately

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Memory leak from unbounded buffer | Low | High | ConcurrentQueue with batch size limit, periodic flush |
| Database write contention | Medium | Medium | Batch writes, async logging, connection pooling |
| Metrics loss on app crash | Medium | Low | Acceptable for analytics (not transactional data) |
| JSONB query performance | Low | Medium | Indexed columns for common queries, JSONB for flexibility |
| Tenant data leakage | Low | High | ITenantOwnedEntity, global query filters |

## Security Considerations

- Tenant isolation via ITenantOwnedEntity and global query filters
- No PII in metrics (PSID is Facebook identifier, not user real identity)
- JSONB field allows flexible metrics without schema changes
- Metrics retention policy (90 days) limits data exposure

## Next Steps

After Phase 7.2 completion:
1. Verify metrics logging in production
2. Check database for metrics records (both variants)
3. Monitor memory usage and flush performance
4. Proceed to Phase 7.3: Metrics API & Reporting

## Unresolved Questions

1. Metrics retention: 90 days or 180 days? (Recommendation: 90 days)
2. Real-time vs batch: Synchronous logging or queue? (Recommendation: Async with buffer)
3. CSAT collection: Add post-conversation survey? (Recommendation: Defer to Phase 8)
