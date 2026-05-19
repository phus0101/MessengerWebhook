# Phase 03: Prompt Cost Optimization & Caching

**Priority**: P1  
**Effort**: 2-3 ngày  
**Status**: Complete  
**Depends on**: Phase 01 (DI fix)

---

## Vấn đề

Research doc: *"Cache là đòn bẩy chi phí lớn nhất sau model routing. Nếu không thiết kế cache ngay từ đầu, hóa đơn sẽ tăng nhanh hơn lượng đơn hàng."*

Hệ thống hiện tại:
- Có `EmbeddingCacheService` (1h TTL) cho vector embeddings
- Có Redis distributed cache
- **Không có** prompt prefix caching (system prompt + personality traits gửi lại mỗi request)
- **Không có** semantic answer cache cho câu hỏi lặp lại (freeship, policy, FAQ)
- **Không có** business data cache với TTL ngắn (tồn kho, giá)

System prompt + personality traits ≈ 2000-4000 tokens × mọi request = chi phí lớn.

---

## Mục tiêu

1. **Gemini Context Caching** — Cache system prompt prefix trên Gemini API
2. **Semantic Answer Cache** — Cache câu trả lời cho câu hỏi phổ biến (freeship, đổi trả, policy)
3. **Business Data Cache** — TTL ngắn cho inventory/price queries
4. **Prompt structure chuẩn** — Sắp xếp prompt theo thứ tự cache-friendly: static parts trước

---

## Thiết kế

### Layer 1: Gemini Context Caching (API-level)

Gemini hỗ trợ [context caching](https://ai.google.dev/gemini-api/docs/caching) cho các model 2.x. Cache system prompt + personality traits, giảm input tokens mỗi request.

```csharp
// GeminiService: tạo cached content 1 lần khi startup
// Dùng cached content name trong mỗi request
public class GeminiContextCacheService
{
    private string? _cachedContentName;
    
    public async Task<string> GetOrCreateCacheAsync()
    {
        if (_cachedContentName != null) return _cachedContentName;
        
        // POST /v1beta/cachedContents với system prompt + personality
        // TTL = 1 giờ (auto-extend khi còn dùng)
        _cachedContentName = await CreateCachedContentAsync();
        return _cachedContentName;
    }
}
```

**Lưu ý**: Context caching chỉ available với Gemini 2.x Flash/Pro, minimum 32k tokens cached content. Nếu system prompt < 32k → dùng Layer 2 thay thế.

**Fallback**: Nếu system prompt < 32k tokens → không dùng API-level cache, dùng prompt prefix cache ở Layer 2.

### Layer 2: Redis Semantic Answer Cache

Cache câu trả lời cho các query phổ biến:

```csharp
public interface ISemanticAnswerCache
{
    Task<string?> GetAsync(string queryNormalized, string cacheKey);
    Task SetAsync(string queryNormalized, string cacheKey, string answer, TimeSpan ttl);
}
```

Cache keys:
- `policy:freeship:{tenantId}` — TTL 6h (chính sách không đổi thường xuyên)
- `policy:return:{tenantId}` — TTL 6h
- `product:faq:{productCode}:{questionHash}` — TTL 1h

Trigger cache hit khi `SubIntent == PolicyQuestion || ShippingQuestion` và câu hỏi match normalized pattern.

### Layer 3: Business Data Cache (TTL ngắn)

Extend `ResultCacheService` hiện có:
- `inventory:{tenantId}:{sku}` — TTL 5 phút
- `price:{tenantId}:{sku}` — TTL 15 phút
- `gift_policy:{tenantId}` — TTL 30 phút

### Prompt Structure Optimization

Sắp xếp lại thứ tự prompt theo research doc — **bắt buộc 7 sections theo đúng thứ tự**:

```
1. [STATIC]  System policy        (beauty-consultant-system-prompt.txt phần policy)
2. [STATIC]  Brand voice          (personality-traits.txt)
3. [STATIC]  Sales objective      (sales-closer-system-prompt.txt)
4. [STATIC]  Tool schemas         (signature các grounded actions: draftOrder, queryInventory)
5. [DYNAMIC] Retrieved RAG facts  (top-k chunks từ Pinecone, đứng trước summary để LLM ground)
6. [DYNAMIC] Conversation summary (Phase 04 output, nếu có)
7. [DYNAMIC] Recent turns + user  (ephemeral window 6 turns + current message)
```

**Rationale**: 4 sections đầu identical mọi request ⇒ cache-friendly prefix. Section 5-7 thay đổi ⇒ không cache.

`SalesPromptBuilder` cần refactor:
- Method `BuildSystemPrompt(StateContext, ragContext, summary)` ráp theo đúng thứ tự
- Constant `PROMPT_SECTION_DELIMITER = "\n---\n"` giữa sections để debug dễ
- Test: hash 4 sections đầu phải identical với 2 request khác user → confirm cache key stable

---

## Files cần tạo

- `Services/AI/Cache/GeminiContextCacheService.cs` — Gemini API-level context cache
- `Services/AI/Cache/IGeminiContextCacheService.cs`
- `Services/Cache/SemanticAnswerCache.cs` — Redis-backed semantic answer cache
- `Services/Cache/ISemanticAnswerCache.cs`

## Files cần sửa

- `Services/AI/GeminiService.cs` — dùng cached content name trong requests
- `Services/Sales/Reply/SalesReplyOrchestrator.cs` — check semantic cache trước khi gọi LLM
- `Services/Sales/Prompt/SalesPromptBuilder.cs` — reorder prompt sections
- `Configuration/ServiceRegistration/AiServicesRegistration.cs` — đăng ký cache services
- `Configuration/ServiceRegistration/CacheServicesRegistration.cs` — thêm semantic answer cache

---

## Implementation Steps

### Step 1: Kiểm tra system prompt size (0.25 ngày)

```csharp
// Đọc system prompt + personality traits, đếm tokens
// Nếu < 32k tokens → Gemini context caching không applicable
// Nếu >= 32k → implement GeminiContextCacheService
var tokenCount = await geminiService.CountTokensAsync(systemPrompt + personality);
```

### Step 2: Semantic Answer Cache cho policy/FAQ (1 ngày)

`SemanticAnswerCache` dùng `IDistributedCache` (Redis đã có):

```csharp
public async Task<string?> GetPolicyCacheAsync(string subIntent, string tenantId)
{
    var key = $"semantic:{subIntent}:{tenantId}";
    var cached = await _cache.GetStringAsync(key);
    return cached; // null = cache miss
}
```

Tích hợp vào `SalesReplyOrchestrator.GenerateAsync`:
```csharp
// Trước khi gọi Gemini:
if (request.SubIntent?.Category == SubIntentCategory.PolicyQuestion)
{
    var cached = await _semanticCache.GetPolicyCacheAsync(...);
    if (cached != null) return cached;
}
// ... gọi Gemini
// Sau khi có response:
await _semanticCache.SetAsync(..., ttl: TimeSpan.FromHours(6));
```

### Step 3: Business Data Cache TTL ngắn (0.5 ngày)

Extend `ResultCacheService` với typed methods cho inventory/price:

```csharp
Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl);
```

Wrap `IProductRepository` calls trong `SalesContextResolver`.

### Step 4: Prompt reorder (0.25 ngày)

Review `SalesPromptBuilder.BuildSystemPrompt()` — đảm bảo static sections (policy, brand voice, tools) đứng trước dynamic sections (RAG context, history, current message).

### Step 5: Metrics (0.25 ngày)

Log cache hit/miss để track hiệu quả:
```
[SemanticCache] Hit key={key} SubIntent={subIntent}
[SemanticCache] Miss key={key} — calling LLM
```

Sau 1 tuần có data → tính % hit rate.

---

## Todo

- [ ] Đếm token size của system prompt hiện tại
- [ ] Quyết định dùng Gemini context cache API hay không
- [ ] Tạo ISemanticAnswerCache + SemanticAnswerCache
- [ ] Tích hợp semantic cache vào SalesReplyOrchestrator
- [ ] Extend ResultCacheService với inventory/price TTL
- [ ] Reorder SalesPromptBuilder sections
- [ ] Đăng ký services trong DI
- [ ] Unit test cache hit/miss behavior
- [ ] Build + tests pass

---

## Success Criteria

- Cache hit rate ≥ 50% cho policy/FAQ questions sau 1 tuần production
- System prompt sections đúng thứ tự (static trước dynamic)
- Log cache hits visible trong Seq
- 0 regression trong conversation quality

---

## Risk

- **Gemini context cache min 32k**: Nếu prompt nhỏ hơn → API reject. Fallback = bỏ API-level cache, chỉ dùng semantic cache
- **Redis key collision**: Dùng `{tenantId}` trong key để tránh cross-tenant leak
- **Stale semantic cache**: Khi policy thay đổi → cần invalidation mechanism. Phase này: manual invalidation via cache key delete. Auto-invalidation là future work
- **Semantic cache over-cache**: Câu hỏi product-specific (giá SP A) không nên cache lâu. Chỉ cache policy/FAQ generic
