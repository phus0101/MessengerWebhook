---
title: "RAG Architecture Implementation"
description: "Implement Retrieval-Augmented Generation with Vertex AI embeddings, Pinecone vector DB, and multi-layer caching"
status: pending
priority: P1
effort: 6 weeks
branch: master
tags: [rag, ai, optimization, cost-reduction, vertex-ai, pinecone]
created: 2026-04-01
---

# RAG Architecture Implementation Plan

## Executive Summary

**Goal**: Reduce token cost by 91.9% ($752→$60/month) and scale to 100+ products without prompt bloat.

**Approach**: Replace full-catalog prompts with RAG (Retrieval-Augmented Generation) using hybrid search (vector + keyword), Vietnamese-optimized embeddings, and multi-layer caching.

**Expected Impact**:
- 91.9% cost reduction ($752→$60/month)
- 75% latency improvement (3s→0.75s)
- Scale to 100+ products with flat cost
- 100% Vietnamese accuracy (validated)

## Context

**Current Issues** (from root-cause-260401-1036):
- Bot doesn't remember returning customers
- Contradictory responses (says "found info" but asks again)
- Doesn't answer freeship questions
- Prompt bloat when adding products (20K tokens per request)

**Research Validation**:
- text-embedding-004: 100% accuracy on Vietnamese queries (researcher-260401-1311)
- Pinecone: Best .NET SDK, <50ms latency (researcher-260401-1113)
- Hybrid search: 17% better precision vs vector-only (researcher-260401-1113)

## Architecture Overview

```
User Query
    ↓
[Embedding Cache (Redis)] ← 90% hit rate
    ↓ (cache miss)
[Vertex AI text-embedding-004] ← 768-dim, Vietnamese-optimized
    ↓
[Hybrid Search]
    ├─ Vector Search (Pinecone) ← semantic matching
    └─ Keyword Search (BM25) ← exact product codes
    ↓
[RRF Fusion] ← merge results
    ↓
[Result Cache (Redis)] ← 70% hit rate
    ↓
[Context Assembly] ← top 3-5 products
    ↓
[Gemini 1.5 Pro + Prompt Caching] ← 74% token reduction
    ↓
[Response Cache (Redis)] ← 50% hit rate
    ↓
Response
```

## Tech Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Vector DB | Pinecone Serverless | Managed, mature .NET SDK, <50ms latency |
| Embedding | text-embedding-004 (Vertex AI) | 100% Vietnamese accuracy, task-optimized, GA status |
| Keyword Search | Pinecone Sparse-Dense | Built-in hybrid, no extra infrastructure |
| Cache | Azure Cache for Redis | Native Azure integration, distributed cache |
| LLM | Gemini 1.5 Pro | Existing, add prompt caching |

## Implementation Phases

| Phase | Focus | Duration | Deliverable |
|-------|-------|----------|-------------|
| [Phase 1](phase-01-vertex-ai-setup.md) | Vertex AI Setup | Week 1 | Working embedding service |
| [Phase 2](phase-02-vector-database.md) | Vector Database | Week 1-2 | Indexed products in Pinecone |
| [Phase 3](phase-03-hybrid-search.md) | Hybrid Search | Week 2-3 | RRF fusion with 17% better precision |
| [Phase 4](phase-04-caching-layer.md) | Caching Layer | Week 3-4 | 4× faster responses |
| [Phase 5](phase-05-integration.md) | Integration | Week 4-5 | Production-ready RAG chatbot |
| [Phase 6](phase-06-optimization.md) | Optimization | Week 5-6 | Validated metrics, <200ms latency |

## Phase Status

- [x] Phase 1: Vertex AI Setup (COMPLETED - IEmbeddingService interface created)
- [x] Phase 2: Vector Database (COMPLETED - Pinecone integration with ProductEmbeddingPipeline)
- [x] Phase 3: Hybrid Search (COMPLETED 2026-04-02 - RRF fusion, 37/37 tests passing, code review 9.2/10)
- [x] Phase 4: Caching Layer (COMPLETED 2026-04-02 - Redis multi-layer cache, 32/32 tests passing)
- [ ] Phase 5: Integration
- [ ] Phase 6: Optimization

## Dependencies

```
Phase 1 (Vertex AI) → Phase 2 (Vector DB) → Phase 3 (Hybrid Search)
                                                    ↓
Phase 4 (Caching) ←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←┘
    ↓
Phase 5 (Integration) → Phase 6 (Optimization)
```

**Critical Path**: Phase 1 → Phase 2 → Phase 3 → Phase 5 (can parallelize Phase 4)

## Success Metrics

### Performance
- Retrieval precision: >85% (relevant products in top-5)
- Retrieval recall: >90% (find all relevant products)
- Query latency: <100ms (p95)
- End-to-end latency: <1s (p95)

### Cost
- Token reduction: >90% vs current
- Monthly cost: <$100 for 1000 conversations/day
- Cost per conversation: <$0.01

### Quality
- User satisfaction: >4.5/5
- Conversation success rate: >80%
- Hallucination rate: <5%

### Operational
- Cache hit rate: >70% (embeddings), >50% (responses)
- Uptime: >99.5%
- Error rate: <1%

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Vietnamese embedding quality | Low | High | Already validated 100% accuracy |
| Latency regression | Low | Medium | Pinecone <50ms SLA, aggressive caching |
| Cache staleness | Medium | Low | Short TTLs (5-15min), invalidate on updates |
| Cost overrun | Low | Medium | Set spending limits, monitor daily |
| Pinecone downtime | Low | High | Fallback to full-context mode, circuit breaker |
| Embedding API rate limits | Low | Medium | Aggressive caching, batch requests, exponential backoff |

## Rollback Strategy

Each phase has independent rollback:
- **Phase 1-2**: Delete Vertex AI/Pinecone resources, no code changes deployed
- **Phase 3-4**: Feature flag to disable RAG, revert to full-context prompts
- **Phase 5**: Gradual rollout (10%→50%→100%), instant rollback via feature flag
- **Phase 6**: Revert config changes, no code rollback needed

## Data Migration

**Existing Data**: No migration needed (RAG is additive)
- Product catalog: Read from existing `Products` table
- Embeddings: New `ProductEmbedding` table (no impact on existing schema)
- Conversations: Continue using existing `ConversationSession` table

**Backwards Compatibility**: 100% (RAG is opt-in via feature flag)

## Test Matrix

| Test Type | Coverage | Tools |
|-----------|----------|-------|
| Unit Tests | Embedding service, RRF fusion, cache logic | xUnit, Moq |
| Integration Tests | Pinecone queries, Redis cache, Vertex AI API | Testcontainers (Postgres + Redis) |
| E2E Tests | Full RAG pipeline with real queries | Vietnamese test dataset (13 queries) |
| Load Tests | 1000 concurrent queries, cache hit rates | k6 or Artillery |
| A/B Tests | RAG vs full-context on 100 real conversations | Feature flag + metrics |

## File Ownership

| Phase | Files Created | Files Modified | Owner |
|-------|--------------|----------------|-------|
| Phase 1 | `Services/AI/VertexAIEmbeddingService.cs` | `Program.cs` (DI) | Phase 1 |
| Phase 2 | `Services/VectorSearch/PineconeService.cs`, `Data/Entities/ProductEmbedding.cs` | `MessengerBotDbContext.cs` | Phase 2 |
| Phase 3 | `Services/VectorSearch/HybridSearchService.cs`, `Services/VectorSearch/RRFFusion.cs` | None | Phase 3 |
| Phase 4 | `Services/Cache/EmbeddingCacheService.cs`, `Services/Cache/ResultCacheService.cs` | `appsettings.json` | Phase 4 |
| Phase 5 | None | `Services/AI/GeminiService.cs`, `StateMachine/Handlers/SalesStateHandlerBase.cs` | Phase 5 |
| Phase 6 | `Monitoring/RAGMetricsCollector.cs` | `appsettings.json` | Phase 6 |

**No Conflicts**: Each phase owns distinct files, Phase 5 is only phase modifying existing services.

## Unresolved Questions

1. **Vertex AI Region**: asia-southeast1 (Singapore) vs asia-east1 (Taiwan) for lowest latency from Vietnam?
2. **Product Update Frequency**: How often do product details change? Affects cache TTL strategy.
3. **Query Complexity**: Are users asking multi-product comparisons ("iPhone vs Samsung")? May need query decomposition.
4. **Compliance**: Any data residency requirements for product data? Affects Pinecone region selection.
5. **Conversation Context**: Should RAG consider previous messages for retrieval? Affects context assembly logic.
6. **Vertex AI Costs**: Actual monthly cost for 10K embedding requests on Vertex AI?

## Next Steps

1. Review plan with team
2. Get approval for Google Cloud / Vertex AI account setup
3. Get approval for Pinecone account setup
4. Start Phase 1: Vertex AI Setup
5. Create feature flag for gradual rollout
