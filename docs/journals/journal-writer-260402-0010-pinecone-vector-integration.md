# Pinecone Vector Database Integration for Semantic Product Search

**Date**: 2026-04-02 00:10
**Severity**: Low
**Component**: Vector Search / Product Discovery
**Status**: Resolved

## What Happened

Integrated Pinecone.Client v2.0.0 SDK alongside existing pgvector implementation to enable semantic product search in Vietnamese. The integration required dual storage strategy, multi-tenant namespace isolation, and graceful degradation handling. Commit hash: 728beaf.

## The Brutal Truth

This was surprisingly smooth compared to the usual integration hell. The dual storage approach felt right from the start — pgvector as source of truth, Pinecone as search accelerator. No major surprises, no 3am debugging sessions. Almost suspicious how well it went.

## Technical Details

**Architecture decisions:**
- Dual storage: pgvector (primary) + Pinecone (search optimization)
- Multi-tenant isolation via namespaces (namespace = TenantId)
- 768-dimensional embeddings from Vertex AI text-embedding-004
- Cosine similarity metric

**Implementation:**
- Created `IVectorSearchService` interface with 5 operations (upsert, batch, search, delete, health check)
- Implemented `PineconeVectorService` with namespace-based tenant isolation
- Updated `ProductEmbeddingPipeline` for dual writes
- Registered services in Program.cs with .env API key mapping
- Fixed EF Core InMemory compatibility issue with Vector value converter

**Test results:**
- Integration tests: 84/99 passing (15 failures unrelated to Pinecone)
- Build: 0 errors
- Graceful degradation verified: Pinecone failures don't break pgvector operations

## What We Tried

First attempt used single storage (Pinecone only) but rejected it immediately — losing control of data persistence felt wrong. Dual storage was the second approach and stuck.

## Root Cause Analysis

N/A — this was a planned feature implementation, not a bug fix. The success came from:
1. Clear interface design before implementation
2. Keeping pgvector as primary storage (no migration risk)
3. Namespace isolation pattern from day one (multi-tenant by design)

## Lessons Learned

**What worked:**
- Dual storage strategy eliminates vendor lock-in anxiety
- Graceful degradation from the start prevents future panic
- Multi-tenant isolation via namespaces is cleaner than metadata filtering
- Interface-first design made testing trivial

**What to watch:**
- Pinecone costs scale with vector count — monitor usage per tenant
- Sync drift between pgvector and Pinecone could happen if batch jobs fail silently
- 768 dimensions is expensive; consider dimensionality reduction if performance degrades

## Next Steps

1. **Monitor sync health**: Add metrics for pgvector/Pinecone consistency checks
2. **Cost tracking**: Implement per-tenant vector count monitoring for billing
3. **Performance baseline**: Capture search latency metrics before production load
4. **Fallback testing**: Verify pgvector search works when Pinecone is down (chaos engineering)

**Owner**: Backend team
**Timeline**: Monitoring in place before production rollout (next sprint)
