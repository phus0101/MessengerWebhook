---
title: "Phase 2.5: RAG Layer Tests Implementation Plan"
description: "Unit & integration tests for GeminiEmbeddingService and VectorSearchRepository"
status: pending
priority: P1
effort: 3h
branch: master
tags: [testing, rag, embeddings, vector-search, phase-2.5]
created: 2026-03-22
---

# Phase 2.5: RAG Layer Tests Implementation Plan

## Context

Test coverage for RAG layer components:
- **GeminiEmbeddingService**: HTTP-based embedding generation (unit tests)
- **VectorSearchRepository**: pgvector similarity search (integration tests)

**Constraints:**
- Token budget: 57% used (keep concise)
- Existing patterns: xUnit, Moq, FluentAssertions
- No HttpClient mocking helper exists yet
- No pgvector test infrastructure

## File Structure

```
tests/
├── MessengerWebhook.UnitTests/
│   ├── Services/
│   │   └── AI/
│   │       └── GeminiEmbeddingServiceTests.cs          [NEW]
│   └── Helpers/
│       └── MockHttpMessageHandler.cs                   [NEW]
└── MessengerWebhook.IntegrationTests/
    ├── Data/
    │   └── Repositories/
    │       └── VectorSearchRepositoryTests.cs          [NEW]
    └── Fixtures/
        └── DatabaseFixture.cs                          [UPDATE - add pgvector]
```

## Phase 1: Unit Tests - GeminiEmbeddingService (1h)

### 1.1 Create MockHttpMessageHandler (15min)
**File:** `tests/MessengerWebhook.UnitTests/Helpers/MockHttpMessageHandler.cs`

```csharp
// Reusable HttpClient mock for testing HTTP services
// - Queue responses for sequential calls
// - Capture request details for assertions
// - Support success/error scenarios
```

**Why:** No existing pattern for mocking HttpClient; needed for all HTTP service tests.

### 1.2 GeminiEmbeddingService Unit Tests (45min)
**File:** `tests/MessengerWebhook.UnitTests/Services/AI/GeminiEmbeddingServiceTests.cs`

**Test cases:**

```csharp
// GenerateAsync
- GenerateAsync_ValidText_ReturnsEmbedding
- GenerateAsync_EmptyText_ThrowsArgumentException
- GenerateAsync_ApiError_ThrowsHttpRequestException
- GenerateAsync_EmptyResult_ThrowsInvalidOperationException

// GenerateBatchAsync
- GenerateBatchAsync_MultipleTexts_ReturnsEmbeddings
- GenerateBatchAsync_Over100Texts_BatchesCorrectly (101 texts → 2 API calls)
- GenerateBatchAsync_EmptyList_ReturnsEmptyList
- GenerateBatchAsync_ApiError_ThrowsHttpRequestException
```

**Mocking strategy:**
- Mock HttpClient via MockHttpMessageHandler
- Mock ILogger<T> via Moq
- Mock IOptions<GeminiOptions> with test config

## Phase 2: Integration Tests - VectorSearchRepository (2h)

### 2.1 Update DatabaseFixture for pgvector (30min)
**File:** `tests/MessengerWebhook.IntegrationTests/Fixtures/DatabaseFixture.cs`

**Options:**
1. **In-memory SQLite** (fast, no pgvector support) ❌
2. **Testcontainers + pgvector** (real Postgres, slower) ✅ RECOMMENDED
3. **Shared test DB** (fast, state pollution risk) ⚠️

**Decision:** Use Testcontainers for isolation + real pgvector.

**Setup:**
```csharp
// Add NuGet: Testcontainers.PostgreSql
// Spin up postgres:16 with pgvector extension
// Run migrations
// Seed test products with embeddings
```

### 2.2 VectorSearchRepository Integration Tests (1.5h)
**File:** `tests/MessengerWebhook.IntegrationTests/Data/Repositories/VectorSearchRepositoryTests.cs`

**Test cases:**

```csharp
// SearchSimilarProductsAsync
- SearchSimilarProductsAsync_ValidEmbedding_ReturnsProducts
- SearchSimilarProductsAsync_HighThreshold_FiltersResults
- SearchSimilarProductsAsync_InvalidDimensions_ThrowsArgumentException (767 dims)
- SearchSimilarProductsAsync_NullEmbedding_ThrowsArgumentNullException
- SearchSimilarProductsAsync_NoMatches_ReturnsEmptyList

// UpdateProductEmbeddingAsync
- UpdateProductEmbeddingAsync_ValidData_UpdatesEmbedding
- UpdateProductEmbeddingAsync_ProductNotFound_ThrowsInvalidOperationException
- UpdateProductEmbeddingAsync_InvalidDimensions_ThrowsArgumentException
- UpdateProductEmbeddingAsync_NullEmbedding_ThrowsArgumentNullException
```

**Test data:**
```csharp
// Seed 3 products with known embeddings
// Product A: [0.9, 0.1, ...] (768 dims)
// Product B: [0.1, 0.9, ...] (similar to query)
// Product C: [0.5, 0.5, ...] (medium similarity)
// Query: [0.2, 0.8, ...] (should match B > C > A)
```

## Phase 3: NuGet Packages (5min)

**UnitTests.csproj:**
```xml
<!-- Already has: xUnit, Moq, FluentAssertions -->
<!-- No new packages needed -->
```

**IntegrationTests.csproj:**
```xml
<PackageReference Include="Testcontainers.PostgreSql" Version="3.10.0" />
```

## Implementation Order

1. ✅ Create MockHttpMessageHandler helper
2. ✅ Write GeminiEmbeddingService unit tests
3. ✅ Add Testcontainers NuGet to IntegrationTests
4. ✅ Update DatabaseFixture with pgvector container
5. ✅ Write VectorSearchRepository integration tests
6. ✅ Run all tests, verify coverage

## Success Criteria

- [ ] All unit tests pass (GeminiEmbeddingService)
- [ ] All integration tests pass (VectorSearchRepository)
- [ ] No real API calls in unit tests
- [ ] Integration tests use isolated Testcontainers DB
- [ ] Tests run in <30s total
- [ ] Code coverage >80% for both classes

## Risk Assessment

**Risk:** Testcontainers slow on Windows
**Mitigation:** Use shared container fixture (IClassFixture), reuse across tests

**Risk:** pgvector extension not installed in container
**Mitigation:** Use `ankane/pgvector:latest` image OR run `CREATE EXTENSION vector` in setup

**Risk:** Embedding dimension mismatches (768 vs 1536)
**Mitigation:** Use constants, validate in test setup

## Next Steps

1. Delegate to `implementer` agent with this plan
2. After implementation, delegate to `tester` agent to run tests
3. If tests fail, fix and re-test
4. Delegate to `code-reviewer` agent for quality check
5. Update `docs/project-roadmap.md` (Phase 2.5 → Complete)

## Unresolved Questions

- Should we mock ILogger in unit tests or use NullLogger? (Prefer NullLogger for simplicity)
- Do we need performance benchmarks for vector search? (Not in scope for Phase 2.5)
