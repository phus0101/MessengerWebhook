# Phase 2: REST API Endpoints

## Priority
P1 (blocks Phase 3)

## Status
pending

## Overview

Add REST API endpoints for starting indexing jobs and polling progress. Endpoints follow existing admin API patterns with authorization and CSRF protection.

## Key Insights

- Existing pattern: `/admin/api/vector-search/index-all` already exists (line 248)
- Current implementation starts job but returns immediately (no job ID)
- Need to modify existing endpoint to return job ID
- Add new endpoint for status polling

## Requirements

### Functional
- Start indexing job (modify existing endpoint)
- Poll job status by ID
- Prevent concurrent indexing jobs (lock mechanism)
- Return structured progress data

### Non-functional
- Response time: <50ms for status queries
- Idempotent job start (return existing job if running)
- Proper HTTP status codes (200, 400, 409, 404)

## Architecture

### Endpoint Design

```
POST /admin/api/vector-search/index-all
→ Returns: { jobId: "guid", message: "Indexing started" }
→ Status: 200 OK or 409 Conflict (if job already running)

GET /admin/api/vector-search/index-status/{jobId}
→ Returns: IndexingJob DTO
→ Status: 200 OK or 404 Not Found
```

### DTO Models

```csharp
public record StartIndexingResponse(Guid JobId, string Message);

public record IndexingStatusResponse(
    Guid JobId,
    string Status,
    int TotalProducts,
    int IndexedProducts,
    int ProgressPercentage,
    string? CurrentProductId,
    string? CurrentProductName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);
```

## Related Code Files

**Modify:**
- `src/MessengerWebhook/Endpoints/AdminOperationsEndpointExtensions.cs` (lines 248-278)

## Implementation Steps

1. Add lock mechanism to prevent concurrent jobs:
   ```csharp
   private static readonly SemaphoreSlim _indexingLock = new(1, 1);
   ```

2. Modify existing `POST /admin/api/vector-search/index-all`:
   - Check if job already running (via tracker.GetActiveJobs())
   - Return 409 Conflict if job exists
   - Create job ID via tracker.CreateJob()
   - Pass job ID to pipeline.IndexAllProductsAsync()
   - Return job ID in response

3. Add new `GET /admin/api/vector-search/index-status/{jobId}`:
   - Query tracker.GetJob(jobId)
   - Return 404 if not found
   - Map to DTO and return

4. Update error handling:
   - Catch exceptions in background task
   - Call tracker.FailJob() with error message

## Todo List

- [ ] Add SemaphoreSlim for job locking
- [ ] Create `StartIndexingResponse` record
- [ ] Create `IndexingStatusResponse` record
- [ ] Modify POST endpoint to return job ID
- [ ] Add concurrent job check (return 409)
- [ ] Pass job ID to ProductEmbeddingPipeline
- [ ] Add GET endpoint for status polling
- [ ] Map IndexingJob to DTO
- [ ] Add error handling in background task
- [ ] Test with Postman/curl

## Success Criteria

- POST returns job ID immediately (<100ms)
- GET returns current progress (<50ms)
- Concurrent POST requests return 409 Conflict
- Job status persists across multiple GET requests
- Error states properly reported

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Concurrent job start race | SemaphoreSlim lock + active job check |
| Background task exception not caught | Try/catch with FailJob call |
| Job ID not found (expired) | Return 404, client handles gracefully |

## Security Considerations

- Existing authorization middleware applies (RequireAuthorization)
- CSRF token required for POST (existing validation)
- Job ID uses GUID (no enumeration attack)

## API Examples

### Start Indexing

```bash
POST /admin/api/vector-search/index-all
X-CSRF-TOKEN: <token>
Cookie: <auth-cookie>

Response 200:
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Indexing started in background"
}

Response 409 (if already running):
{
  "error": "Indexing job already in progress"
}
```

### Poll Status

```bash
GET /admin/api/vector-search/index-status/3fa85f64-5717-4562-b3fc-2c963f66afa6
Cookie: <auth-cookie>

Response 200:
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Running",
  "totalProducts": 150,
  "indexedProducts": 45,
  "progressPercentage": 30,
  "currentProductId": "PROD-123",
  "currentProductName": "Serum Vitamin C",
  "startedAt": "2026-04-02T09:52:00Z",
  "completedAt": null,
  "errorMessage": null
}

Response 404 (if not found):
{
  "error": "Job not found or expired"
}
```

## Next Steps

After completion:
- Proceed to Phase 3 (UI components)
- Test endpoints manually
- Document API in Swagger/OpenAPI (optional)
