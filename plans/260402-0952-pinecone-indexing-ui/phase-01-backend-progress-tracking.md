# Phase 1: Backend Progress Tracking Service

## Priority
P1 (blocks Phase 2)

## Status
pending

## Overview

Implement in-memory progress tracking service to monitor Pinecone indexing jobs. Service stores job state, progress metrics, and provides thread-safe access for concurrent reads/writes.

## Key Insights

- Existing `ProductEmbeddingPipeline.IndexAllProductsAsync` processes in batches of 10
- Current implementation logs progress but doesn't expose it
- Need thread-safe in-memory store (ConcurrentDictionary)
- Jobs should auto-expire after 1 hour to prevent memory leak

## Requirements

### Functional
- Track multiple indexing jobs by unique ID (GUID)
- Store: total products, indexed count, current product name/ID, status, errors
- Thread-safe read/write operations
- Report progress percentage (indexed/total * 100)
- Support job states: NotStarted, Running, Completed, Failed, Cancelled

### Non-functional
- Max 100 jobs in memory (FIFO eviction)
- Job TTL: 1 hour after completion
- Response time: <10ms for status queries
- Thread-safe for concurrent access

## Architecture

### Data Model

```csharp
public class IndexingJob
{
    public Guid JobId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public IndexingStatus Status { get; set; }
    public int TotalProducts { get; set; }
    public int IndexedProducts { get; set; }
    public string? CurrentProductId { get; set; }
    public string? CurrentProductName { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProgressPercentage => TotalProducts > 0
        ? (int)((double)IndexedProducts / TotalProducts * 100)
        : 0;
}

public enum IndexingStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

### Service Interface

```csharp
public interface IIndexingProgressTracker
{
    Guid CreateJob(int totalProducts);
    void UpdateProgress(Guid jobId, int indexedCount, string? currentProductId, string? currentProductName);
    void CompleteJob(Guid jobId);
    void FailJob(Guid jobId, string errorMessage);
    IndexingJob? GetJob(Guid jobId);
    IReadOnlyList<IndexingJob> GetActiveJobs();
}
```

## Related Code Files

**New:**
- `src/MessengerWebhook/Services/VectorSearch/IndexingProgressTracker.cs`
- `src/MessengerWebhook/Services/VectorSearch/IIndexingProgressTracker.cs`

**Modify:**
- `src/MessengerWebhook/Services/VectorSearch/ProductEmbeddingPipeline.cs` (inject tracker, report progress)
- `src/MessengerWebhook/Program.cs` (register as singleton)

## Implementation Steps

1. Create `IIndexingProgressTracker` interface with methods above
2. Implement `IndexingProgressTracker` class:
   - Use `ConcurrentDictionary<Guid, IndexingJob>` for storage
   - Implement TTL cleanup via background timer (check every 5 min)
   - Add max capacity check (100 jobs, remove oldest completed)
3. Modify `ProductEmbeddingPipeline`:
   - Inject `IIndexingProgressTracker` in constructor
   - Add `IndexAllProductsAsync(Guid jobId)` overload
   - Call `UpdateProgress` after each batch
   - Call `CompleteJob` or `FailJob` in try/finally
4. Register service in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IIndexingProgressTracker, IndexingProgressTracker>();
   ```

## Todo List

- [ ] Create `IndexingJob` model class
- [ ] Create `IIndexingProgressTracker` interface
- [ ] Implement `IndexingProgressTracker` with ConcurrentDictionary
- [ ] Add TTL cleanup background timer
- [ ] Add max capacity enforcement (100 jobs)
- [ ] Modify `ProductEmbeddingPipeline` to accept jobId parameter
- [ ] Add progress reporting calls in batch loop
- [ ] Add try/finally for job completion
- [ ] Register service as singleton in Program.cs
- [ ] Add XML documentation comments

## Success Criteria

- Service tracks multiple jobs concurrently without data corruption
- Progress updates reflect actual indexing state
- Old jobs auto-expire after 1 hour
- Memory usage bounded (max 100 jobs)
- Thread-safe under concurrent access (verified via tests in Phase 4)

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Memory leak from abandoned jobs | TTL cleanup + max capacity |
| Race condition on concurrent updates | ConcurrentDictionary + atomic operations |
| Job ID collision | Use Guid.NewGuid() (collision probability negligible) |

## Security Considerations

- No sensitive data stored (product IDs/names only)
- Jobs not persisted to database (ephemeral state)
- Access control enforced at API layer (Phase 2)

## Next Steps

After completion:
- Proceed to Phase 2 (API endpoints)
- Write unit tests for tracker service (Phase 4)
