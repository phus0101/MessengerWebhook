# Phase 09: RAG Reranking với Cohere Rerank API

**Priority**: P1
**Effort**: 1.5-2 ngày
**Status**: Complete
**Depends on**: Phase 06 (metadata filter — rerank chạy sau filter)

---

## Vấn đề

Research doc: *"Rerank thường cải thiện top-k tốt hơn chỉ dùng cosine similarity"* + *"Cohere Rerank hỗ trợ 100+ ngôn ngữ"*.

Pipeline retrieval hiện tại:
```
query → embedding (Vertex AI) → Pinecone cosine similarity → top-k (k=5-10) → LLM
```

Vấn đề:
- Cosine similarity tốt cho **recall** nhưng kém cho **precision top-1/top-3** với commerce queries
- VD: "kem chống nắng cho da nhạy cảm" → cosine có thể trả về kem dưỡng ẩm có chứa SPF (similar vector) trước kem chống nắng chuyên dụng
- Khách hỏi specific product attribute (texture, ingredient) → cần rerank theo semantic relevance + product type match

---

## Mục tiêu

1. **Cohere Rerank v3** trong RAG pipeline — chạy sau Pinecone retrieval, trước LLM
2. **Multilingual model** (`rerank-multilingual-v3.0`) — hỗ trợ tiếng Việt
3. **Top-K shrink**: Pinecone top-20 → Rerank top-5 → LLM. Vẫn giữ recall, tăng precision
4. **Cost control**: Rerank ~$2/1k searches → cache theo `query_hash + tenant`, TTL 1h
5. **Fallback**: nếu Cohere API down → skip rerank, dùng raw Pinecone top-5 (Phase 02 resilience pattern)

---

## Thiết kế

### Pipeline mới

```
query
  → embedding (Vertex AI)
  → Pinecone (k=20, filter by metadata)  ← Phase 06
  → Cohere Rerank (top_n=5)              ← Phase 09 (mới)
  → LLM prompt assembly                  ← Phase 03 (cache-friendly order)
```

### Cohere SDK

```bash
dotnet add package Cohere --version 0.x  # latest .NET SDK
```

Verify package: Cohere chính thức có .NET SDK (kiểm tra NuGet trước khi commit version).

### CohereRerankService

```csharp
public interface IRerankService
{
    Task<IReadOnlyList<RankedDocument>> RerankAsync(
        string query,
        IReadOnlyList<RankableDocument> candidates,
        int topN,
        CancellationToken ct = default);
}

public record RankableDocument(string Id, string Text, Dictionary<string, object>? Metadata = null);
public record RankedDocument(string Id, string Text, double RelevanceScore, Dictionary<string, object>? Metadata);

public class CohereRerankService : IRerankService
{
    private readonly CohereClient _client;
    private readonly IDistributedCache _cache;
    private readonly CohereOptions _options;
    private readonly ILogger<CohereRerankService> _logger;

    public async Task<IReadOnlyList<RankedDocument>> RerankAsync(
        string query, IReadOnlyList<RankableDocument> candidates, int topN, CancellationToken ct)
    {
        if (candidates.Count <= topN)
            return candidates.Select(c => new RankedDocument(c.Id, c.Text, 1.0, c.Metadata)).ToList();

        // Cache key: hash(query) + tenant + topN
        var cacheKey = BuildCacheKey(query, _tenantContext.TenantId, topN);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogInformation("RerankCache Hit Query={QueryHash}", cacheKey);
            return JsonSerializer.Deserialize<List<RankedDocument>>(cached)!;
        }

        try
        {
            var response = await _client.V2.RerankAsync(new V2RerankRequest
            {
                Model = _options.RerankModel, // "rerank-multilingual-v3.0"
                Query = query,
                Documents = candidates.Select(c => c.Text).ToList(),
                TopN = topN,
                ReturnDocuments = false
            }, cancellationToken: ct);

            var ranked = response.Results
                .Select(r => new RankedDocument(
                    candidates[r.Index].Id,
                    candidates[r.Index].Text,
                    r.RelevanceScore,
                    candidates[r.Index].Metadata))
                .ToList();

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(ranked),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, ct);

            return ranked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cohere Rerank failed — falling back to original order");
            // Fallback: raw Pinecone order
            return candidates.Take(topN).Select(c => new RankedDocument(c.Id, c.Text, 0.0, c.Metadata)).ToList();
        }
    }
}
```

### Tích hợp HybridSearchService / RAGService

```csharp
// HybridSearchService (Phase 06 extended)
var pineconeResults = await _vectorService.SearchAsync(embedding, topK: 20, filters: metadataFilter, ct);

var rankable = pineconeResults.Select(r => new RankableDocument(r.Id, r.Text, r.Metadata)).ToList();
var reranked = await _rerankService.RerankAsync(query, rankable, topN: 5, ct);

return reranked.Select(r => new SearchResult { ... }).ToList();
```

### Config

```json
"Cohere": {
    "ApiKey": "${COHERE_API_KEY}",
    "RerankModel": "rerank-multilingual-v3.0",
    "TimeoutMs": 3000,
    "CacheTtlMinutes": 60,
    "Enabled": true
}
```

`COHERE_API_KEY` thêm vào `.env.example` và Docker compose env.

---

## Files cần tạo

- `Services/RAG/Reranking/IRerankService.cs`
- `Services/RAG/Reranking/CohereRerankService.cs`
- `Services/RAG/Reranking/Models/RankableDocument.cs`
- `Services/RAG/Reranking/Models/RankedDocument.cs`
- `Configuration/CohereOptions.cs`

## Files cần sửa

- `Services/RAG/HybridSearchService.cs` — call `IRerankService` sau Pinecone
- `Services/VectorSearch/PineconeVectorService.cs` — increase default topK 5→20 (rerank shrink lại)
- `Configuration/ServiceRegistration/RAGServicesRegistration.cs` — register `CohereRerankService` + HttpClient
- `MessengerWebhook.csproj` — add Cohere NuGet package
- `.env.example` — `COHERE_API_KEY=`
- `appsettings.json` — `Cohere` section

---

## Implementation Steps

### Step 1: Cohere account + API key (0.1 ngày)

- Đăng ký Cohere Production tier ($)
- Generate API key, lưu vào `.env`
- Test cURL với `rerank-multilingual-v3.0` model + tiếng Việt sample

### Step 2: IRerankService + CohereRerankService (0.5 ngày)

Implement theo Thiết kế. Cache-aware, fail-safe (fallback to raw order).

### Step 3: Tích hợp HybridSearchService (0.5 ngày)

Pinecone topK 5 → 20. Pass results vào rerank. Return top-5 reranked.

### Step 4: Cost + Latency budget (0.25 ngày)

Cohere Rerank latency p95 ~ 200-400ms. Plus Pinecone topK=20 (vs 5) ~ 100ms extra.
Total RAG latency budget tăng từ ~500ms → ~900ms. Verify không vi phạm SLO 5s reply (Production Stabilization Phase 06).

Cost calc: $2/1k searches × cache hit rate dự kiến 40% → effective $1.2/1k. Phase 00 baseline sẽ track.

### Step 5: A/B test ramp (0.25 ngày)

- Feature flag `Cohere:Enabled` per tenant
- Canary 1 tenant 24h → 10 tenant 48h → 100% rollout
- Monitor: `consultationRejectionCount`, `draftOrderCreated rate`, response latency p95

### Step 6: Tests + Observability (0.25 ngày)

Unit test: cache hit/miss, fallback when API fail.
Log event:
```
RerankCompleted Provider=Cohere Model=multilingual-v3 ElapsedMs={} CandidateCount={} TopN={} CacheHit={}
```

---

## Todo

- [x] Đăng ký Cohere account + API key
- [x] Add Cohere NuGet package vào csproj
- [x] Tạo IRerankService + CohereRerankService
- [x] Tạo CohereOptions binding
- [x] Tích hợp vào HybridSearchService
- [x] Pinecone topK 5 → 20
- [x] Cache key strategy + Redis integration
- [x] Fallback on API failure
- [x] Log RerankCompleted events
- [x] Unit test cache + fallback
- [ ] Canary 1 tenant 24h
- [x] Build + tests pass

---

## Success Criteria

- Cohere Rerank active trong production
- RAG top-3 precision improvement (subjective: human review 20 conversations before/after)
- Latency p95 retrieval stage tăng ≤ 400ms (acceptable trong overall 5s SLO)
- Rerank cache hit rate ≥ 30%
- Cohere cost ≤ $50/month cho 1000 tenant baseline traffic

---

## Risk

- **Cohere API latency spike**: Timeout 3s + fallback to raw Pinecone order — không block reply
- **Cost runaway**: Cache 1h TTL + per-tenant rate limit. Alert nếu daily cost > $5
- **Tiếng Việt rerank quality không như English**: `rerank-multilingual-v3.0` hỗ trợ 100+ ngôn ngữ nhưng cần verify với golden test Vietnamese queries
- **Vendor lock**: Cohere only. Mitigation: `IRerankService` interface ⇒ swap sang cross-encoder local sau nếu cần

---

## Unresolved questions

1. Cohere .NET SDK version stable nhất? Cần verify NuGet trước commit
2. Rerank cache key — chỉ query + tenant đủ chưa, hay cần thêm `filter_hash` (vì filter thay đổi → candidates thay đổi)?
3. A/B test metric chính cho rerank effectiveness — `draftOrderCreated rate` đủ không hay cần human eval?
