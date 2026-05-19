---
phase: 7.3
title: "Metrics API & Reporting"
effort: 3h
status: pending
dependencies: [7.2]
---

# Phase 7.3: Metrics API & Reporting

## Context

Build API endpoints to query and aggregate metrics data for A/B test analysis. Provides data access for dashboards and external analytics tools.

**Related Files**:
- Phase 7.2: Metrics collection (provides data source)
- `Data/Entities/ConversationMetric.cs` - Metrics entity

## Overview

**Priority**: P2  
**Status**: Pending  
**Effort**: 3 hours  
**Dependencies**: Phase 7.2 (needs metrics data)

## Key Insights

- API-first approach: Build endpoints before dashboard (dashboard can be deferred)
- Aggregation at query time: No pre-computed rollups (YAGNI for MVP)
- Tenant-aware: All queries filtered by TenantId
- Time-range filtering: Essential for A/B test analysis

## Requirements

### Functional
- GET /api/metrics/summary - Overall A/B test summary
- GET /api/metrics/variants - Compare control vs treatment
- GET /api/metrics/pipeline - Pipeline performance breakdown
- GET /api/metrics/conversations - Conversation outcome metrics
- Query params: startDate, endDate, tenantId (optional for admin)

### Non-Functional
- Query latency: <500ms for 10K metrics
- Pagination: 100 items per page
- Caching: 5min cache for aggregated results
- Authorization: Admin-only endpoints

## Architecture

### API Endpoints

**1. GET /api/metrics/summary**
```json
{
  "period": { "start": "2026-04-08", "end": "2026-04-22" },
  "totalSessions": 1250,
  "totalMessages": 8420,
  "variants": {
    "control": { "sessions": 625, "messages": 4210 },
    "treatment": { "sessions": 625, "messages": 4210 }
  },
  "avgResponseTimeMs": {
    "control": 850,
    "treatment": 920
  }
}
```

**2. GET /api/metrics/variants**
```json
{
  "control": {
    "sessions": 625,
    "avgMessagesPerSession": 6.7,
    "completionRate": 0.68,
    "escalationRate": 0.12,
    "abandonmentRate": 0.20,
    "avgResponseTimeMs": 850
  },
  "treatment": {
    "sessions": 625,
    "avgMessagesPerSession": 7.2,
    "completionRate": 0.75,
    "escalationRate": 0.08,
    "abandonmentRate": 0.17,
    "avgResponseTimeMs": 920,
    "emotionAccuracy": 0.87,
    "toneMatchingRate": 0.92,
    "validationPassRate": 0.94
  }
}
```

**3. GET /api/metrics/pipeline**
```json
{
  "avgLatencyMs": {
    "emotionDetection": 18,
    "toneMatching": 9,
    "contextAnalysis": 28,
    "smallTalkDetection": 14,
    "responseValidation": 23,
    "total": 92
  },
  "p95LatencyMs": {
    "total": 98
  }
}
```

## Related Code Files

### Files to Create

**1. `Controllers/MetricsController.cs`**
**2. `Services/Metrics/IMetricsAggregationService.cs`**
**3. `Services/Metrics/MetricsAggregationService.cs`**
**4. `Services/Metrics/Models/MetricsSummary.cs`**
**5. `Services/Metrics/Models/VariantComparison.cs`**
**6. `Services/Metrics/Models/PipelinePerformance.cs`**

### Files to Modify

**1. `Program.cs`** - Register services and map endpoints

## Implementation Steps

1. **Create metrics models** (30min)
2. **Implement aggregation service** (60min)
3. **Create API controller** (45min)
4. **Add caching layer** (20min)
5. **Compile and verify** (15min)
6. **Manual testing** (30min)

## Todo List

- [ ] Create `Services/Metrics/Models/MetricsSummary.cs`
- [ ] Create `Services/Metrics/Models/VariantComparison.cs`
- [ ] Create `Services/Metrics/Models/PipelinePerformance.cs`
- [ ] Create `Services/Metrics/IMetricsAggregationService.cs`
- [ ] Create `Services/Metrics/MetricsAggregationService.cs`
- [ ] Create `Controllers/MetricsController.cs`
- [ ] Add authorization attributes
- [ ] Register services in Program.cs
- [ ] Add response caching middleware
- [ ] Run `dotnet build` and fix errors
- [ ] Manual test: /api/metrics/summary
- [ ] Manual test: /api/metrics/variants
- [ ] Manual test: /api/metrics/pipeline
- [ ] Manual test: date range filtering
- [ ] Manual test: tenant isolation

## Success Criteria

**Technical**:
- All endpoints return valid JSON
- Query latency <500ms for 10K metrics
- Tenant isolation enforced
- Caching working (5min TTL)
- Authorization enforced (admin-only)

**Business**:
- A/B test comparison data accessible
- Pipeline performance metrics visible
- Data sufficient for statistical analysis

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Query performance degradation | Medium | Medium | Add indexes, implement pagination, cache results |
| Unauthorized data access | Low | High | Admin authorization, tenant filtering |
| Cache invalidation issues | Low | Low | Short TTL (5min), acceptable staleness |

## Security Considerations

- Admin-only endpoints (require authentication)
- Tenant isolation via global query filters
- No PII exposed in API responses
- Rate limiting on metrics endpoints

## Next Steps

After Phase 7.3 completion:
1. Test API endpoints with real data
2. Verify tenant isolation
3. Proceed to Phase 7.4: Testing & Validation
4. Consider dashboard implementation (post-MVP)

## Unresolved Questions

1. Dashboard implementation: Build custom React dashboard or defer? (Recommendation: Defer, API-first)
2. Export functionality: Add CSV/Excel export? (Recommendation: Defer to Phase 8)
3. Real-time metrics: WebSocket updates or polling? (Recommendation: Polling sufficient for MVP)
