# Phase 1: Core Service Implementation

**Status:** 🔴 Not Started
**Priority:** High
**Estimated Effort:** 2-3 days

## Overview

Tạo interface và implementation cho Pinecone Vector Search service với correct v2.0.0 API syntax. Update ProductEmbeddingPipeline để sử dụng service mới.

## Context Links

- Research report: `plans/reports/researcher-260401-2237-pinecone-client-v2-api.md`
- Existing: `Services/VectorSearch/ProductEmbeddingPipeline.cs`
- Existing: `Configuration/PineconeOptions.cs`

## Key Insights

**Correct v2.0.0 API:**
- Use `Metadata` class (NOT MetadataMap)
- Direct assignment: `metadata["key"] = value` (NO MetadataValue.From())
- Request objects: UpsertRequest, QueryRequest, DeleteRequest
- Namespace parameter for multi-tenant isolation

**Multi-Tenant Pattern:**
- Namespace = TenantId
- Vector ID = `{tenantId}-{productId}`
- Metadata includes tenant_id for verification

## Related Code Files

**To Create:**
- `Services/VectorSearch/IVectorSearchService.cs` - Interface definition
- `Services/VectorSearch/PineconeVectorService.cs` - Implementation

**To Modify:**
- `Services/VectorSearch/ProductEmbeddingPipeline.cs` - Uncomment Pinecone calls

## Implementation Steps

### 1. Create IVectorSearchService Interface

**File:** `Services/VectorSearch/IVectorSearchService.cs`

```csharp
namespace MessengerWebhook.Services.VectorSearch;

public interface IVectorSearchService
{
    Task<UpsertResult> UpsertProductAsync(
        string productId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    Task<UpsertResult> UpsertBatchAsync(
        List<(string productId, float[] embedding, Dictionary<string, object> metadata)> products,
        CancellationToken cancellationToken = default);

    Task<List<ProductSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    Task DeleteProductAsync(
        string productId,
        CancellationToken cancellationToken = default);

    Task DeleteBatchAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default);
}

public class UpsertResult
{
    public int UpsertedCount { get; set; }
    public List<string> FailedIds { get; set; } = new();
}

public class ProductSearchResult
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public float Score { get; set; }
}
```

### 2. Implement PineconeVectorService

**File:** `Services/VectorSearch/PineconeVectorService.cs`

**Key components:**
- Constructor: Inject PineconeClient, ITenantContext, IOptions<PineconeOptions>, ILogger
- Helper: `ConvertToMetadata(Dictionary<string, object>)` - Convert dict to Metadata class
- UpsertProductAsync: Single product upsert với retry logic
- UpsertBatchAsync: Batch upsert (chunks of 100) với ParallelUpsertException handling
- SearchSimilarAsync: Query với namespace isolation và metadata filters
- DeleteProductAsync/DeleteBatchAsync: Delete operations

**Critical implementation details:**
```csharp
// Metadata conversion
private Metadata ConvertToMetadata(Dictionary<string, object> dict)
{
    var metadata = new Metadata();
    foreach (var kvp in dict)
    {
        metadata[kvp.Key] = kvp.Value;
    }
    return metadata;
}

// Upsert pattern
var request = new UpsertRequest
{
    Vectors = new List<Vector>
    {
        new Vector
        {
            Id = $"{tenantId}-{productId}",
            Values = embedding,
            Metadata = ConvertToMetadata(metadata)
        }
    },
    Namespace = tenantId
};
var response = await _index.UpsertAsync(request);

// Query pattern
var request = new QueryRequest
{
    Namespace = tenantId,
    Vector = queryEmbedding,
    TopK = topK,
    IncludeMetadata = true,
    IncludeValues = false,
    Filter = filters != null ? ConvertToMetadata(filters) : null
};
var response = await _index.QueryAsync(request);
```

### 3. Update ProductEmbeddingPipeline

**File:** `Services/VectorSearch/ProductEmbeddingPipeline.cs`

**Changes:**
- Line 12: Uncomment `private readonly IVectorSearchService _vectorSearch;`
- Line 18: Uncomment parameter in constructor
- Line 23: Uncomment assignment
- Lines 68-73: Uncomment Pinecone upsert call, wrap in try-catch
- Lines 100-108: Uncomment batch upsert call, wrap in try-catch

**Pattern for graceful degradation:**
```csharp
// After pgvector save succeeds
try
{
    var metadata = BuildMetadata(product);
    await _vectorSearch.UpsertProductAsync(
        productId,
        embedding,
        metadata,
        cancellationToken);
    _logger.LogInformation("Indexed to Pinecone: {ProductId}", productId);
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Pinecone upsert failed for {ProductId}. pgvector succeeded.",
        productId);
    // Don't throw - dual storage strategy
}
```

## Todo List

- [ ] Create IVectorSearchService interface với DTOs
- [ ] Implement PineconeVectorService constructor và dependencies
- [ ] Implement ConvertToMetadata helper
- [ ] Implement UpsertProductAsync với retry logic
- [ ] Implement UpsertBatchAsync với chunking và error handling
- [ ] Implement SearchSimilarAsync với filters
- [ ] Implement DeleteProductAsync và DeleteBatchAsync
- [ ] Update ProductEmbeddingPipeline - uncomment lines
- [ ] Add try-catch wrappers cho graceful degradation
- [ ] Test compilation

## Success Criteria

- ✅ IVectorSearchService interface defined với clear contracts
- ✅ PineconeVectorService implements all methods correctly
- ✅ Metadata conversion works với v2.0.0 API
- ✅ Multi-tenant isolation via namespaces
- ✅ ProductEmbeddingPipeline uses service
- ✅ Graceful degradation khi Pinecone fails
- ✅ Project builds without errors

## Risk Assessment

**Risks:**
- API syntax errors từ v2.0.0 breaking changes
- Metadata type conversion issues
- Namespace isolation không hoạt động
- Retry logic không handle transient errors

**Mitigation:**
- Follow research report syntax exactly
- Test metadata conversion với sample data
- Verify namespace parameter trong all requests
- Implement exponential backoff cho retries

## Security Considerations

- API key loaded từ .env (not hardcoded)
- Tenant isolation via namespaces (prevent data leakage)
- Metadata không chứa sensitive data
- Error messages không expose internal details

## Next Steps

After Phase 1 completion:
- Phase 2: Register services trong Program.cs
- Phase 3: Integration tests
