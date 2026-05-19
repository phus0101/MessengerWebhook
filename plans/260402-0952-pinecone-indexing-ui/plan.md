---
title: "Pinecone Indexing UI with Real-time Progress Tracking"
description: "Admin UI button and real-time progress tracking for Pinecone product indexing"
status: pending
priority: P2
effort: 6h
branch: master
tags: [admin-ui, pinecone, real-time, progress-tracking]
created: 2026-04-02
---

# Pinecone Indexing UI with Real-time Progress Tracking

## Overview

Add UI controls and real-time progress tracking for Pinecone product indexing in admin dashboard. Users can trigger indexing via button and monitor progress with live updates showing total/indexed/remaining products, current product being processed, and progress percentage.

## Architecture Decision: Polling vs WebSocket

**Choice: HTTP Polling (KISS principle)**

**Why:**
- Simpler implementation (no SignalR infrastructure needed)
- Existing admin API pattern already uses REST
- Indexing is infrequent operation (not high-frequency updates)
- Progress updates every 2-3 seconds sufficient for UX
- Easier to debug and maintain

**Trade-offs:**
- Slightly higher server load (acceptable for admin-only feature)
- 1-2 second latency vs real-time (acceptable for this use case)

**Future:** Can migrate to SignalR if needed without breaking API contract

## Data Flow

```
User clicks "Index Products" button
    ↓
POST /admin/api/vector-search/index-all (starts background job)
    ↓
Returns job ID immediately
    ↓
UI polls GET /admin/api/vector-search/index-status/{jobId} every 2s
    ↓
Backend reads from in-memory progress tracker
    ↓
UI updates progress bar, stats, current product
    ↓
Job completes → polling stops → shows success/error
```

## Phases

| Phase | Status | Effort | Description |
|-------|--------|--------|-------------|
| [Phase 1](phase-01-backend-progress-tracking.md) | pending | 2h | Backend progress tracking service |
| [Phase 2](phase-02-api-endpoints.md) | pending | 1h | REST API endpoints for indexing control |
| [Phase 3](phase-03-ui-components.md) | pending | 2h | React UI components with progress display |
| [Phase 4](phase-04-integration-testing.md) | pending | 1h | Integration tests and error handling |

## Dependencies

- Phase 2 depends on Phase 1 (needs progress tracker)
- Phase 3 depends on Phase 2 (needs API endpoints)
- Phase 4 depends on Phase 3 (needs complete feature)

## Success Criteria

- [ ] Admin can trigger indexing via button
- [ ] Progress shows: total, indexed, remaining, current product, percentage
- [ ] Progress updates every 2-3 seconds during indexing
- [ ] UI handles errors gracefully (network, indexing failures)
- [ ] Multiple concurrent indexing jobs prevented
- [ ] Progress persists across page refresh (job ID in URL)
- [ ] Tests cover happy path and error scenarios

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Memory leak from abandoned jobs | Medium | Medium | TTL-based cleanup, max 100 jobs in memory |
| Race condition on job start | Low | Medium | Lock mechanism prevents concurrent indexing |
| UI polling after job complete | Low | Low | Stop polling on terminal states |
| Large product count (10k+) | Medium | Low | Batch processing already implemented |

## Rollback Plan

- Phase 1: Remove progress tracker service registration
- Phase 2: Remove API endpoints from routing
- Phase 3: Hide UI button via feature flag
- Phase 4: No rollback needed (tests only)

## File Ownership

**Backend (Phase 1-2):**
- `Services/VectorSearch/IndexingProgressTracker.cs` (new)
- `Services/VectorSearch/ProductEmbeddingPipeline.cs` (modify)
- `Endpoints/AdminOperationsEndpointExtensions.cs` (modify)
- `Program.cs` (modify - service registration)

**Frontend (Phase 3):**
- `AdminApp/src/pages/vector-search-page.tsx` (new)
- `AdminApp/src/lib/api.ts` (modify)
- `AdminApp/src/lib/types.ts` (modify)
- `AdminApp/src/components/layout.tsx` (modify - add nav link)

**Tests (Phase 4):**
- `tests/MessengerWebhook.IntegrationTests/VectorSearch/IndexingProgressTests.cs` (new)

## Security Considerations

- All endpoints require admin authorization (existing middleware)
- CSRF token validation on POST requests (existing)
- Job ID uses GUID to prevent enumeration
- Progress data scoped to authenticated user's tenant

## Next Steps

1. Review plan with stakeholders
2. Start Phase 1: Backend progress tracking
3. Parallel research: Review existing admin UI patterns for consistency
