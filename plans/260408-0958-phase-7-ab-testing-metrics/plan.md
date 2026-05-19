---
title: "Phase 7: A/B Testing & Metrics Infrastructure"
description: "A/B testing framework and metrics collection for naturalness pipeline evaluation"
status: completed
priority: P1
effort: 25h
branch: master
tags: [metrics, ab-testing, analytics, monitoring, phase-7]
created: 2026-04-08
completed: 2026-04-09
---

# Phase 7: A/B Testing & Metrics Infrastructure

## Executive Summary

Build A/B testing infrastructure and metrics collection to measure naturalness pipeline impact vs baseline. Enables data-driven evaluation of Phases 0-6 improvements and guides future optimizations.

**Timeline**: 25 hours (6 phases)  
**Status**: Completed ✅ (All 6 sub-phases delivered)  
**Completion Date**: 2026-04-09  
**Scope**: A/B test framework, metrics collection, API endpoints, custom dashboard, CSAT survey, comprehensive testing

## Context

**Completed Foundation (Phases 0-6)**:
- 5 naturalness services: Emotion, Tone, Context, SmallTalk, Validation
- Full pipeline integrated in `SalesStateHandlerBase.BuildNaturalReplyAsync`
- 36 integration tests passing (100%)
- Pipeline overhead: <100ms (p95)

**Business Goal**: Measure naturalness pipeline impact vs baseline to validate ROI and guide future improvements.

## Problem Statement

Without A/B testing and metrics:
- Cannot quantify naturalness pipeline impact on business metrics
- No data to justify continued investment in naturalness features
- Cannot identify which pipeline components provide most value
- No visibility into performance regressions

## Success Criteria

- A/B test framework assigns users consistently (by PSID)
- Metrics collected for 100% of conversations
- Zero performance regression (<100ms overhead maintained)
- All tests passing (target: 58+ tests total)
- A/B test running for 2 weeks with statistical significance (n>100 per variant)
- Dashboard/API showing treatment vs control comparison

## Architecture Overview

```
User Message → A/B Assignment (by PSID)
    ↓
Control (50%): Skip pipeline → Direct AI response
Treatment (50%): Full pipeline → Emotion → Tone → Context → SmallTalk → Validation
    ↓
Log Metrics → ConversationMetricsService → Database
    ↓
Metrics API → Dashboard (optional) / External Analytics
```

## Phase Breakdown

| Phase | Description | Effort | Status | Dependencies |
|-------|-------------|--------|--------|--------------|
| 7.1 | A/B Test Infrastructure | 4h | Completed ✅ | None |
| 7.2 | Metrics Collection | 5h | Completed ✅ | 7.1 |
| 7.3 | Metrics API & Reporting | 3h | Completed ✅ | 7.2 |
| 7.4 | Testing & Validation | 4h | Completed ✅ | 7.1, 7.2, 7.3 |
| 7.5 | Custom Dashboard | 6h | Completed ✅ | 7.3 |
| 7.6 | CSAT Survey | 3h | Completed ✅ | 7.2 |

**Total Effort**: 25 hours

## Phase Details

### Phase 7.1: A/B Test Infrastructure (4h)

**Goal**: Deterministic user assignment to control/treatment variants

**Key Components**:
- `ABTestService` with SHA256-based assignment
- `conversation_sessions.ab_test_variant` column
- Feature flag for instant rollback
- Integration in `SalesStateHandlerBase`

**Success Criteria**:
- 50/50 split achieved (verified with 100 test PSIDs)
- Assignment deterministic (same PSID → same variant)
- Control group skips pipeline (verified in logs)
- Treatment group runs pipeline (verified in logs)

**Files**: [See phase-7.1-ab-test-infrastructure.md](./phase-7.1-ab-test-infrastructure.md)

### Phase 7.2: Metrics Collection (5h)

**Goal**: Track pipeline performance and conversation outcomes

**Key Components**:
- `ConversationMetricsService` with async buffering
- `conversation_metrics` table with JSONB flexibility
- Background flush service (100 items or 60s)
- Metrics capture at each pipeline step

**Success Criteria**:
- Metrics logged for 100% of messages
- Async logging <10ms overhead
- Batch flush working (60s interval or 100 items)
- Tenant isolation maintained

**Files**: [See phase-7.2-metrics-collection.md](./phase-7.2-metrics-collection.md)

### Phase 7.3: Metrics API & Reporting (3h)

**Goal**: Query and aggregate metrics for A/B test analysis

**Key Components**:
- `MetricsController` with 3 endpoints (summary, variants, pipeline)
- `MetricsAggregationService` for data processing
- Response caching (5min TTL)
- Admin-only authorization

**Success Criteria**:
- All endpoints return valid JSON
- Query latency <500ms for 10K metrics
- Tenant isolation enforced
- A/B test comparison data accessible

**Files**: [See phase-7.3-metrics-api-reporting.md](./phase-7.3-metrics-api-reporting.md)

### Phase 7.4: Testing & Validation (4h)

**Goal**: Comprehensive testing before production deployment

**Key Components**:
- Unit tests (12 tests): ABTest, Metrics, Aggregation services
- Integration tests (8 tests): A/B assignment, metrics collection, API
- E2E tests (2 tests): Control and treatment user journeys
- Performance tests (4 tests): Latency benchmarks

**Success Criteria**:
- All 22 new tests passing (36 existing + 22 = 58 total)
- Test coverage 85%+ for Phase 7 code
- No flaky tests (3 consecutive runs pass)
- CI/CD pipeline green

**Files**: [See phase-7.4-testing-validation.md](./phase-7.4-testing-validation.md)

### Phase 7.5: Custom Dashboard (6h)

**Goal**: Build React admin dashboard to visualize A/B test results

**Key Components**:
- React + TypeScript + Vite (existing AdminApp structure)
- 3 dashboard views: A/B Test Summary, Pipeline Performance, Conversation Outcomes
- Real-time updates (polling every 30s)
- Date range picker and CSV export
- Responsive design (desktop + tablet)

**Success Criteria**:
- Dashboard loads in <2s
- Charts render correctly with real data
- Date range filtering works
- CSV export functional
- Responsive on desktop + tablet

**Files**: [See phase-7.5-custom-dashboard.md](./phase-7.5-custom-dashboard.md)

### Phase 7.6: CSAT Survey (3h)

**Goal**: Add post-conversation CSAT survey to measure customer satisfaction

**Key Components**:
- Survey trigger: 5 minutes after conversation completion
- Survey format: 5-star rating + optional text feedback
- Survey delivery: Facebook Messenger quick reply buttons
- Survey storage: `conversation_surveys` table
- Survey metrics: Track CSAT score by variant

**Success Criteria**:
- Survey sent 5min after conversation completion
- Survey response stored correctly
- CSAT metrics visible in dashboard (Phase 7.5)
- Survey doesn't interrupt active conversations

**Files**: [See phase-7.6-csat-survey.md](./phase-7.6-csat-survey.md)

## Data Flow

### A/B Assignment Flow
```
PSID: "123456789" → Hash(PSID + seed) % 100 → 0-49: Control, 50-99: Treatment
    ↓
Store in ConversationSession.ABTestVariant
    ↓
Consistent assignment across all messages in session
```

### Metrics Collection Flow
```
Message Processed → Extract Metrics:
  - Variant (control/treatment)
  - Emotion detected (if treatment)
  - Tone profile (if treatment)
  - Journey stage
  - Response validated (if treatment)
  - Pipeline latency
  - Conversation outcome (completed/abandoned/escalated)
    ↓
ConversationMetricsService.LogAsync()
    ↓
In-memory buffer (ConcurrentQueue)
    ↓
Batch flush to conversation_metrics table
```

## Database Schema

### New Table: conversation_metrics

```sql
CREATE TABLE conversation_metrics (
    id UUID PRIMARY KEY,
    session_id UUID REFERENCES conversation_sessions(id),
    facebook_psid VARCHAR(255),
    facebook_page_id VARCHAR(255),
    tenant_id UUID,
    
    -- A/B Test Context
    variant VARCHAR(20), -- 'control' or 'treatment'
    
    -- Pipeline Metrics (NULL for control)
    emotion_detected VARCHAR(50),
    emotion_confidence DECIMAL(3,2),
    tone_profile VARCHAR(50),
    journey_stage VARCHAR(50),
    small_talk_detected BOOLEAN,
    response_validated BOOLEAN,
    validation_issues TEXT[],
    
    -- Performance Metrics
    pipeline_latency_ms INT,
    total_response_time_ms INT,
    
    -- Conversation Outcomes
    message_count INT DEFAULT 1,
    conversation_completed BOOLEAN,
    escalated_to_human BOOLEAN,
    user_abandoned BOOLEAN,
    
    -- Flexible Storage
    metrics_json JSONB,
    
    created_at TIMESTAMP DEFAULT NOW()
);
```

### Update: conversation_sessions table

```sql
ALTER TABLE conversation_sessions 
ADD COLUMN ab_test_variant VARCHAR(20);
```

## Configuration

### appsettings.json

```json
{
  "ABTesting": {
    "Enabled": true,
    "TreatmentPercentage": 50,
    "HashSeed": "naturalness-pipeline-v1",
    "StartDate": "2026-04-08",
    "EndDate": "2026-04-22"
  },
  "Metrics": {
    "Enabled": true,
    "LogEveryMessage": true,
    "BatchSize": 100,
    "FlushIntervalSeconds": 60
  }
}
```

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Performance regression | Medium | High | Async metrics logging, batch writes, performance tests |
| Inconsistent A/B assignment | Low | High | Hash-based deterministic assignment, unit tests |
| Metrics storage bloat | Medium | Medium | TTL policy (90 days), JSONB compression, indexes |
| Control group worse UX | Low | Medium | Monitor escalation rate, kill switch via config |
| Statistical insignificance | Medium | Medium | 2-week duration, n>100 per variant, power analysis |

## Rollback Plan

**Phase 7.1 Rollback**: Set `ABTesting.Enabled = false` → All users get treatment (full pipeline)  
**Phase 7.2 Rollback**: Set `Metrics.Enabled = false` → No metrics logged, zero overhead  
**Phase 7.3 Rollback**: Remove API endpoints, no data loss  
**Phase 7.4 Rollback**: N/A (tests only)

**Database Rollback**:
```sql
DROP TABLE conversation_metrics;
ALTER TABLE conversation_sessions DROP COLUMN ab_test_variant;
```

## Backwards Compatibility

- Existing sessions without `ab_test_variant` → Assign on next message
- Existing code paths unchanged (pipeline always runs, A/B just skips for control)
- Metrics collection optional (feature flag)
- No breaking changes to public APIs

## Test Matrix

### Unit Tests (12 tests)
- ABTestService: Assignment consistency, hash distribution, config handling
- ConversationMetricsService: Metric logging, batch writes, error handling
- MetricsAggregationService: Aggregation logic, statistical calculations

### Integration Tests (8 tests)
- A/B assignment persisted to database
- Metrics logged for control and treatment
- Pipeline skipped for control variant
- Full pipeline executed for treatment variant
- Metrics API returns correct aggregations

### E2E Tests (2 tests)
- Control user journey (no pipeline)
- Treatment user journey (full pipeline)

**Total**: 22 new tests (36 existing + 22 = 58 total)

## File Ownership

**Phase 7.1 (A/B Test Infrastructure)**:
- `Services/ABTesting/ABTestService.cs` (new)
- `Services/ABTesting/IABTestService.cs` (new)
- `StateMachine/Handlers/SalesStateHandlerBase.cs` (modify)
- `Data/Entities/ConversationSession.cs` (modify)
- `Data/Migrations/AddABTestVariant.cs` (new)

**Phase 7.2 (Metrics Collection)**:
- `Services/Metrics/ConversationMetricsService.cs` (new)
- `Services/Metrics/IConversationMetricsService.cs` (new)
- `Services/Metrics/Models/ConversationMetricData.cs` (new)
- `Data/Entities/ConversationMetric.cs` (new)
- `Data/Migrations/AddConversationMetrics.cs` (new)

**Phase 7.3 (Metrics API)**:
- `Controllers/MetricsController.cs` (new)
- `Services/Metrics/MetricsAggregationService.cs` (new)
- `Services/Metrics/Models/MetricsReport.cs` (new)

**Phase 7.4 (Testing)**:
- `tests/MessengerWebhook.UnitTests/Services/ABTestServiceTests.cs` (new)
- `tests/MessengerWebhook.IntegrationTests/Services/MetricsIntegrationTests.cs` (new)

## Dependencies

**External**: None (uses existing infrastructure)  
**Internal**: 
- Phase 7.2 depends on 7.1 (needs A/B assignment)
- Phase 7.3 depends on 7.2 (needs metrics data)
- Phase 7.4 depends on 7.1, 7.2, 7.3 (tests all phases)

## Next Steps

1. Review plan with stakeholders
2. Confirm A/B test duration (2 weeks) and sample size requirements
3. Decide on metrics dashboard approach (custom vs external)
4. Proceed to Phase 7.1 implementation
5. After completion: Run 2-week A/B test
6. Analyze results and iterate

## User Decisions (Resolved)

1. **Metrics retention policy**: ✅ 90 days (approved)
2. **Dashboard implementation**: ✅ Build custom React dashboard (Phase 7.5)
3. **Real-time vs batch metrics**: ✅ Async with in-memory buffer (Phase 7.2)
4. **CSAT collection**: ✅ Add post-conversation survey (Phase 7.6)
5. **Multi-tenant metrics isolation**: ✅ Both, tenant filter in API (Phase 7.3)

## Related Documentation

- `docs/system-architecture.md` (lines 317-681) - Naturalness pipeline architecture
- `plans/260406-2046-bot-naturalness-improvements/plan.md` - Phases 0-6 implementation
- `tests/MessengerWebhook.IntegrationTests/Services/NaturalnessPipelineIntegrationTests.cs` - Existing tests
