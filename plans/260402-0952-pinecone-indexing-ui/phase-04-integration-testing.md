# Phase 4: Integration Testing

## Priority
P2 (quality assurance)

## Status
pending

## Overview

Write integration tests covering happy path, error scenarios, and edge cases. Tests verify backend progress tracking, API endpoints, and error handling.

## Key Insights

- Existing test pattern: `MessengerWebhook.IntegrationTests` project
- Uses `CustomWebApplicationFactory` for test server
- Tests use real database (Postgres) via Docker
- Need to mock Pinecone API calls (external dependency)

## Requirements

### Functional
- Test job creation and progress tracking
- Test concurrent job prevention
- Test job expiration (TTL)
- Test error handling (Pinecone failures)
- Test API endpoints (start, status)

### Non-functional
- Tests run in <30 seconds
- Tests isolated (no shared state)
- Tests deterministic (no flaky tests)

## Architecture

### Test Categories

1. **Progress Tracker Tests** (unit-style)
   - Job creation
   - Progress updates
   - Job completion
   - TTL cleanup
   - Max capacity enforcement

2. **API Endpoint Tests** (integration)
   - Start indexing (success)
   - Start indexing (concurrent conflict)
   - Get status (found)
   - Get status (not found)
   - Authorization required

3. **End-to-End Tests** (integration)
   - Full indexing flow with mocked Pinecone
   - Error handling (Pinecone failure)
   - Progress updates during indexing

## Related Code Files

**New:**
- `tests/MessengerWebhook.IntegrationTests/VectorSearch/IndexingProgressTrackerTests.cs`
- `tests/MessengerWebhook.IntegrationTests/VectorSearch/IndexingApiEndpointTests.cs`
- `tests/MessengerWebhook.IntegrationTests/VectorSearch/IndexingEndToEndTests.cs`

## Implementation Steps

1. **Create IndexingProgressTrackerTests.cs**:
   ```csharp
   [Fact]
   public void CreateJob_ReturnsUniqueJobId()
   [Fact]
   public void UpdateProgress_UpdatesJobState()
   [Fact]
   public void CompleteJob_SetsCompletedStatus()
   [Fact]
   public void FailJob_SetsFailedStatusWithError()
   [Fact]
   public void GetJob_ReturnsNullForExpiredJob()
   [Fact]
   public void MaxCapacity_EvictsOldestCompletedJob()
   ```

2. **Create IndexingApiEndpointTests.cs**:
   ```csharp
   [Fact]
   public async Task StartIndexing_ReturnsJobId()
   [Fact]
   public async Task StartIndexing_ReturnsConcurrentConflict()
   [Fact]
   public async Task GetStatus_ReturnsJobProgress()
   [Fact]
   public async Task GetStatus_Returns404ForUnknownJob()
   [Fact]
   public async Task StartIndexing_RequiresAuthorization()
   [Fact]
   public async Task StartIndexing_RequiresCsrfToken()
   ```

3. **Create IndexingEndToEndTests.cs**:
   ```csharp
   [Fact]
   public async Task IndexingFlow_CompletesSuccessfully()
   [Fact]
   public async Task IndexingFlow_HandlesPartialFailure()
   [Fact]
   public async Task IndexingFlow_UpdatesProgressDuringExecution()
   ```

4. **Mock Pinecone service**:
   - Create `MockVectorSearchService` for tests
   - Simulate delays for realistic progress updates
   - Simulate failures for error testing

## Todo List

- [ ] Create test project structure
- [ ] Implement IndexingProgressTrackerTests
- [ ] Implement IndexingApiEndpointTests
- [ ] Implement IndexingEndToEndTests
- [ ] Create MockVectorSearchService
- [ ] Add test data fixtures (products)
- [ ] Configure test authentication
- [ ] Run all tests and verify pass
- [ ] Check code coverage (aim for >80%)
- [ ] Fix any failing tests

## Success Criteria

- All tests pass consistently
- Code coverage >80% for new code
- Tests complete in <30 seconds
- No flaky tests (run 10 times, all pass)
- Tests cover error scenarios

## Test Matrix

| Scenario | Input | Expected Output |
|----------|-------|-----------------|
| Start indexing (no active job) | POST /index-all | 200, job ID returned |
| Start indexing (job running) | POST /index-all | 409 Conflict |
| Get status (job exists) | GET /status/{id} | 200, progress data |
| Get status (job not found) | GET /status/{id} | 404 Not Found |
| Indexing completes | Wait for completion | Status = Completed |
| Indexing fails | Mock Pinecone error | Status = Failed, error message |
| Job expires | Wait 1 hour | GET returns 404 |
| Max capacity reached | Create 101 jobs | Oldest evicted |

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Tests depend on external Pinecone | Mock IVectorSearchService |
| Tests flaky due to timing | Use deterministic delays, avoid Thread.Sleep |
| Tests slow (>30s) | Reduce product count in fixtures |
| Tests fail in CI/CD | Ensure Docker Postgres available |

## Security Testing

- [ ] Verify unauthorized requests return 401
- [ ] Verify missing CSRF token returns 400
- [ ] Verify job ID enumeration not possible (GUID)

## Performance Testing

- [ ] Verify status query <50ms
- [ ] Verify job creation <100ms
- [ ] Verify progress update <10ms

## Next Steps

After completion:
- Run full test suite
- Review code coverage report
- Fix any gaps in coverage
- Document test patterns for future features
