# Plan: Mock IHybridSearchService trong Integration Tests

## Context

**Vấn đề:** 26 integration tests failed do Pinecone authentication error (401)
- Tests đang cố gắng kết nối đến Pinecone thật nhưng không có valid API key
- Root cause: `IHybridSearchService` không được mock trong `CustomWebApplicationFactory`

**Mục tiêu:** Mock `IHybridSearchService` để integration tests không phụ thuộc vào Pinecone external service

## Quyết định từ Grill Session

1. ✅ Mock `IHybridSearchService` (high-level) thay vì `IVectorSearchService` (low-level)
2. ✅ Sử dụng smart stub với hardcoded keyword mappings
3. ✅ Filter theo `tenant_id` từ filter parameter
4. ✅ Không cần inject DbContext - hardcode mappings

## Architecture

```
Integration Tests
  ↓
CustomWebApplicationFactory
  ↓
TestHybridSearchService (NEW - mock)
  ↓
Returns mock FusedResults based on keywords
```

## Implementation

### File: `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`

**Step 1: Tạo TestHybridSearchService class** (thêm sau line 804)

```csharp
public sealed class TestHybridSearchService : IHybridSearchService
{
    private readonly Guid _primaryTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    
    public Task<List<FusedResult>> SearchAsync(
        string query, 
        int topK = 5, 
        Dictionary<string, object>? filter = null, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<FusedResult>();
        
        // Only return results for primary tenant
        var tenantId = filter?.GetValueOrDefault("tenant_id")?.ToString();
        if (tenantId != _primaryTenantId.ToString())
        {
            return Task.FromResult(results);
        }
        
        var queryLower = query.ToLowerInvariant();
        
        // Map keywords to seeded products
        if (queryLower.Contains("kem chống nắng") || queryLower.Contains("kcn") || queryLower.Contains("chong nang"))
        {
            results.Add(new FusedResult { ProductId = "product-kcn", RRFScore = 0.95f });
        }
        
        if (queryLower.Contains("mặt nạ") || queryLower.Contains("mn") || queryLower.Contains("mat na"))
        {
            results.Add(new FusedResult { ProductId = "product-mn", RRFScore = 0.90f });
        }
        
        if (queryLower.Contains("kem lụa") || queryLower.Contains("kl") || queryLower.Contains("lua"))
        {
            results.Add(new FusedResult { ProductId = "product-kl", RRFScore = 0.85f });
        }
        
        if (queryLower.Contains("combo") || queryLower.Contains("combo 2"))
        {
            results.Add(new FusedResult { ProductId = "product-combo", RRFScore = 0.80f });
        }
        
        return Task.FromResult(results.Take(topK).ToList());
    }
}
```

**Step 2: Register TestHybridSearchService** (thêm vào ConfigureServices, sau line 101)

```csharp
services.RemoveAll<IHybridSearchService>();
services.AddSingleton<IHybridSearchService>(new TestHybridSearchService());
```

## Success Criteria

**Functional:**
1. ✅ Integration tests không gọi Pinecone thật
2. ✅ Tests có thể chạy mà không cần Pinecone API key
3. ✅ RAG flow vẫn hoạt động với mock products
4. ✅ 26 tests failed → pass

**Technical:**
1. ✅ TestHybridSearchService implement `IHybridSearchService`
2. ✅ Filter theo tenant_id đúng
3. ✅ Return mock products dựa trên keywords
4. ✅ Không break existing tests

## Risk Assessment

**Low Risk:**
- Mock service đơn giản, ít logic
- Không ảnh hưởng đến production code
- Chỉ thay đổi test infrastructure

## Notes

- Seeded products trong database: KCN, MN, KL, COMBO_2
- Primary TenantId: `11111111-1111-1111-1111-111111111111`
- Tests vẫn verify business logic, chỉ không test Pinecone connectivity
