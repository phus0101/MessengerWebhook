---
name: Pinecone Integration Test Report
description: Test results for Pinecone Vector Database integration verification
type: test-report
date: 2026-04-01
---

# Pinecone Integration Test Report

## Executive Summary

**Status:** ❌ BLOCKED - Critical EF Core configuration issue

**Root Cause:** `ProductEmbedding.Embedding` property uses `Pgvector.Vector` type which is incompatible with EF Core InMemory provider used in integration tests.

**Impact:** 84/99 integration tests failing, all unit tests passing (88/88).

---

## Test Results Overview

### Unit Tests
- **Total:** 88 tests
- **Passed:** 88 ✅
- **Failed:** 0
- **Skipped:** 0
- **Duration:** ~3 seconds
- **Status:** ✅ ALL PASSING

### Integration Tests
- **Total:** 99 tests
- **Passed:** 11 ✅
- **Failed:** 84 ❌
- **Skipped:** 4
- **Duration:** 33.99 seconds
- **Status:** ❌ BLOCKED

### Overall
- **Total Tests:** 187
- **Pass Rate:** 52.9% (99/187)
- **Critical Blocker:** Yes

---

## Build Status

✅ **Build Successful** with warnings

**Compilation Warnings (Non-blocking):**
1. Grpc.Net.ClientFactory version compatibility warning (recommend upgrade to 2.64.0+)
2. PineconeVectorService.cs nullable warnings (lines 60, 104, 157, 261)
3. Async method warnings in state handlers (AddToCart, Error, OrderPlaced, OrderConfirmation)

**No compilation errors** - code is syntactically correct and compiles successfully.

---

## Critical Issue: EF Core Vector Type Mapping

### Problem

`ProductEmbedding` entity at `Data/Entities/ProductEmbedding.cs`:

```csharp
public class ProductEmbedding : ITenantOwnedEntity
{
    public Vector Embedding { get; set; } = null!;  // ❌ Pgvector.Vector not supported by InMemory provider
}
```

**Error Message:**
```
System.InvalidOperationException: The 'Vector' property 'ProductEmbedding.Embedding'
could not be mapped because the database provider does not support this type.
```

### Why This Happens

- `Pgvector.Vector` is a PostgreSQL-specific type via pgvector extension
- EF Core InMemory provider doesn't support custom value types like Vector
- Integration tests use InMemory provider via `CustomWebApplicationFactory`
- All tests that initialize DbContext fail during model validation

### Affected Tests (84 failures)

All integration test suites affected:
- `ConversationFlowTests` (all tests)
- `WebhookVerificationTests` (all tests)
- `LiveCommentWebhookTests` (all tests)
- `TenantIsolationTests` (all tests)
- `BackgroundProcessingTests` (all tests)
- `GeminiApiIntegrationTests` (all tests)
- `VertexAIEmbeddingIntegrationTests` (all tests)
- `VietnameseBenchmarkTests` (all tests)

---

## Pinecone Integration Status

### ✅ Successfully Implemented

1. **PineconeClient Registration** (`Program.cs`)
   - Singleton PineconeClient configured with API key
   - Proper DI setup

2. **Service Layer** (`Services/VectorSearch/`)
   - `IVectorSearchService` interface defined
   - `PineconeVectorService` implementation complete
   - `ProductEmbeddingPipeline` uncommented and active

3. **Build Verification**
   - Project compiles successfully
   - No syntax errors
   - Pinecone.Client v2.0.0 package integrated

### ❌ Blocked by Test Infrastructure

Cannot verify runtime behavior because:
- Integration tests fail before reaching Pinecone code
- DbContext initialization fails on Vector property
- No test coverage for vector search functionality

---

## Recommendations (Priority Order)

### 1. **CRITICAL: Fix EF Core Vector Mapping**

**Option A: Value Converter (Recommended)**

Add to `MessengerBotDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<ProductEmbedding>()
    .Property(e => e.Embedding)
    .HasConversion(
        v => v.ToArray(),           // Vector → float[]
        v => new Vector(v),         // float[] → Vector
        new ValueComparer<Vector>(
            (v1, v2) => v1!.SequenceEqual(v2!),
            v => v.GetHashCode(),
            v => new Vector(v.ToArray())
        )
    );
```

**Why:** Allows InMemory provider to store Vector as float[] while maintaining Vector type in code.

**Option B: Conditional Configuration**

```csharp
if (!Database.IsInMemory())
{
    modelBuilder.Entity<ProductEmbedding>()
        .Property(e => e.Embedding)
        .HasColumnType("vector(768)");
}
else
{
    // Apply value converter for InMemory
}
```

**Option C: [NotMapped] + Separate Property**

```csharp
[NotMapped]
public Vector Embedding { get; set; }

public float[] EmbeddingArray { get; set; }  // Persisted
```

**Why not:** Breaks encapsulation, requires manual sync.

### 2. **Fix Nullable Warnings in PineconeVectorService**

Lines with CS8629/CS8604 warnings:
- Line 60: `metadata.Score.Value` - add null check
- Line 104: `metadata.Score.Value` - add null check
- Line 157: `response.Matches` - add null check
- Line 261: `tenantId` - add null check

### 3. **Add Pinecone Integration Tests**

After fixing DbContext issue, add:
- `PineconeVectorServiceTests.cs` (unit tests with mocked PineconeClient)
- `PineconeIntegrationTests.cs` (integration tests with real/test index)

Test scenarios:
- Upsert embeddings
- Search by vector
- Filter by tenant
- Batch operations
- Error handling

### 4. **Vietnamese Benchmark Failure**

Separate issue: `VietnameseBenchmarkTests.BenchmarkSuite_AllQueries_100PercentAccuracy`
- Expected: 100% accuracy (13/13 queries)
- Actual: 30.8% accuracy (4/13 queries)
- Most queries returning "Kem chống nắng vật lý Môi Xô" incorrectly

**Root cause:** Likely embedding quality or similarity threshold issue, not related to Pinecone integration.

---

## Next Steps

1. **Immediate:** Implement Option A (Value Converter) in `MessengerBotDbContext`
2. **Verify:** Run `dotnet test` to confirm all tests pass
3. **Fix:** Address nullable warnings in `PineconeVectorService`
4. **Test:** Add unit tests for `PineconeVectorService`
5. **Investigate:** Vietnamese benchmark accuracy issue (separate task)

---

## Unresolved Questions

1. Should we use Testcontainers with PostgreSQL + pgvector for integration tests instead of InMemory?
   - **Pro:** Tests real database behavior, supports Vector type natively
   - **Con:** Slower test execution, requires Docker

2. What is the expected dimension for embeddings (768 or other)?
   - Affects Vector converter and Pinecone index configuration

3. Should `ProductEmbedding` table exist in PostgreSQL or only in Pinecone?
   - Current code suggests dual storage (EF Core + Pinecone)
   - May want to clarify data flow and source of truth

---

**Report Generated:** 2026-04-01 23:47
**Tester:** QA Lead Agent
**Status:** BLOCKED - Awaiting DbContext fix
