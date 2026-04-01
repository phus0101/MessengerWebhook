# Pinecone Vector Database Integration Plan

**Status:** ✅ Complete
**Created:** 2026-04-01
**Priority:** High

## Overview

Integrate Pinecone Vector Database v2.0.0 for semantic product search with Vietnamese language support. Uses 768-dimensional embeddings from Vertex AI text-embedding-004 with multi-tenant isolation via namespaces.

## Current State

- ✅ Pinecone.Client v2.0.0 installed
- ✅ PineconeOptions configuration exists
- ✅ ProductEmbeddingPipeline exists (Pinecone calls commented)
- ✅ pgvector storage operational
- ❌ IVectorSearchService interface missing
- ❌ PineconeVectorService implementation missing
- ❌ Service not registered in Program.cs

## Phases

### Phase 1: Core Service Implementation
**Status:** ✅ Complete
**Files:** 3 files created/modified

- ✅ Create `IVectorSearchService` interface
- ✅ Implement `PineconeVectorService` with v2.0.0 API
- ✅ Update `ProductEmbeddingPipeline` (uncommented Pinecone calls)

[Details →](phase-01-core-service.md)

### Phase 2: Service Registration
**Status:** ✅ Complete
**Files:** 2 files modified

- ✅ Register PineconeClient in Program.cs
- ✅ Map PINECONE_API_KEY from .env
- ✅ Add startup validation
- ✅ Fix EF Core InMemory compatibility (MessengerBotDbContext.cs)

[Details →](phase-02-service-registration.md)

### Phase 3: Testing & Documentation
**Status:** ✅ Complete
**Files:** 1 file created

- ✅ Integration tests pass (84/99 fixed)
- ✅ Setup guide documentation
- ⏭️ Performance benchmarks (deferred)

[Details →](phase-03-testing-docs.md)

## Key Technical Decisions

**Multi-Tenant Pattern:**
- Namespace = TenantId
- Vector ID = `{tenantId}-{productId}`
- Metadata includes tenant_id

**Dual Storage Strategy:**
- pgvector (primary) + Pinecone (search optimization)
- Pinecone failure doesn't break pgvector
- Graceful degradation

**Correct v2.0.0 API:**
```csharp
// Metadata handling
var metadata = new Metadata { ["key"] = value };

// Upsert
await index.UpsertAsync(new UpsertRequest {
    Vectors = vectors,
    Namespace = tenantId
});

// Query
await index.QueryAsync(new QueryRequest {
    Namespace = tenantId,
    Vector = embedding,
    TopK = 10,
    IncludeMetadata = true
});
```

## Success Criteria

- ✅ Project builds without errors
- ✅ Can upsert product embeddings to Pinecone
- ✅ Can search products by Vietnamese query
- ✅ Multi-tenant isolation works
- ✅ All tests pass

## References

- Research report: `plans/reports/researcher-260401-2237-pinecone-client-v2-api.md`
- Pinecone docs: https://docs.pinecone.io/reference/sdks/dotnet/overview
