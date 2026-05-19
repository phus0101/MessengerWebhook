# Phase 2.5: RAG Layer Implementation

**Priority**: Critical
**Status**: Pending
**Duration**: 2 weeks
**Dependencies**: Phase 1, Phase 2

---

## Overview

Implement Retrieval Augmented Generation (RAG) layer using pgvector for semantic product search. Cosmetics require matching products based on ingredients, skin types, and compatibility rules - simple SQL queries insufficient.

---

## Why RAG for Cosmetics?

**Problem**: Customer says "Da tôi dầu và hay bị mụn"
- SQL approach: Filter by `skin_type = 'oily'` → returns 100+ products
- RAG approach: Semantic search → returns top 5 products with BHA, niacinamide, oil-control

**Benefits**:
- Understand natural language queries
- Match ingredients to skin concerns
- Find similar products by formulation
- Recommend based on compatibility rules

---

## Architecture

```
User Query: "Kem dưỡng cho da khô nhạy cảm"
    ↓
1. Generate embedding (text-embedding-004)
    ↓
2. Vector similarity search (pgvector)
    ↓
3. Filter by skin type compatibility
    ↓
4. Rank by ingredient match
    ↓
5. Return top 5 products
```

---

## Implementation Steps

### 1. Install pgvector Extension

```sql
-- PostgreSQL
CREATE EXTENSION IF NOT EXISTS vector;

-- Verify
SELECT * FROM pg_extension WHERE extname = 'vector';
```

### 2. Add Embedding Column to Products

```sql
ALTER TABLE products
ADD COLUMN embedding vector(768);  -- text-embedding-004 dimension

-- Index for fast similarity search
CREATE INDEX ON products
USING ivfflat (embedding vector_cosine_ops)
WITH (lists = 100);
```

**EF Core migration**:
```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector");

migrationBuilder.AddColumn<Vector>(
    name: "embedding",
    table: "products",
    type: "vector(768)",
    nullable: true);

migrationBuilder.Sql(@"
    CREATE INDEX idx_products_embedding
    ON products USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100)");
```

### 3. Create Embedding Service

```csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateAsync(string text, CancellationToken ct = default);
    Task<List<float[]>> GenerateBatchAsync(List<string> texts, CancellationToken ct = default);
}

public class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public async Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        var request = new
        {
            model = "text-embedding-004",
            content = new { parts = new[] { new { text } } }
        };

        var url = "https://generativelanguage.googleapis.com/v1/models/text-embedding-004:embedContent";
        var response = await _httpClient.PostAsJsonAsync(url, request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct);
        return result.Embedding.Values;
    }

    public async Task<List<float[]>> GenerateBatchAsync(List<string> texts, CancellationToken ct)
    {
        // Batch API: up to 100 texts per request
        var batches = texts.Chunk(100);
        var allEmbeddings = new List<float[]>();

        foreach (var batch in batches)
        {
            var request = new
            {
                requests = batch.Select(text => new
                {
                    model = "text-embedding-004",
                    content = new { parts = new[] { new { text } } }
                })
            };

            var url = "https://generativelanguage.googleapis.com/v1/models/text-embedding-004:batchEmbedContents";
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BatchEmbeddingResponse>(ct);
            allEmbeddings.AddRange(result.Embeddings.Select(e => e.Values));
        }

        return allEmbeddings;
    }
}

public class EmbeddingResponse
{
    public EmbeddingData Embedding { get; set; }
}

public class EmbeddingData
{
    public float[] Values { get; set; }
}
```

### 4. Create Vector Search Repository

```csharp
public interface IVectorSearchRepository
{
    Task<List<Product>> SearchBySimilarityAsync(
        float[] queryEmbedding,
        int limit = 10,
        double threshold = 0.7,
        CancellationToken ct = default);

    Task<List<Product>> SearchBySkinProfileAsync(
        float[] queryEmbedding,
        SkinProfile skinProfile,
        int limit = 10,
        CancellationToken ct = default);
}

public class VectorSearchRepository : IVectorSearchRepository
{
    private readonly MessengerBotDbContext _context;
    private readonly ILogger<VectorSearchRepository> _logger;

    public async Task<List<Product>> SearchBySimilarityAsync(
        float[] queryEmbedding,
        int limit,
        double threshold,
        CancellationToken ct)
    {
        // pgvector cosine similarity: 1 - (embedding <=> query)
        var results = await _context.Products
            .Where(p => p.Embedding != null)
            .OrderBy(p => EF.Functions.CosineDistance(p.Embedding, queryEmbedding))
            .Take(limit)
            .ToListAsync(ct);

        // Filter by threshold
        return results
            .Where(p => CosineSimilarity(p.Embedding, queryEmbedding) >= threshold)
            .ToList();
    }

    public async Task<List<Product>> SearchBySkinProfileAsync(
        float[] queryEmbedding,
        SkinProfile skinProfile,
        int limit,
        CancellationToken ct)
    {
        // Semantic search + skin type filter
        var results = await _context.Products
            .Where(p => p.Embedding != null)
            .Where(p => p.Category == ProductCategory.Cosmetics)
            .OrderBy(p => EF.Functions.CosineDistance(p.Embedding, queryEmbedding))
            .Take(limit * 2)  // Get more candidates for filtering
            .ToListAsync(ct);

        // Filter by skin type compatibility
        return results
            .Where(p =>
            {
                var metadata = JsonSerializer.Deserialize<CosmeticsMetadata>(p.MetadataJson);
                return metadata.SkinTypes.Contains(skinProfile.SkinType);
            })
            .Take(limit)
            .ToList();
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        var dot = a.Zip(b, (x, y) => x * y).Sum();
        var magA = Math.Sqrt(a.Sum(x => x * x));
        var magB = Math.Sqrt(b.Sum(x => x * x));
        return dot / (magA * magB);
    }
}
```

### 5. Generate Embeddings for Existing Products

```csharp
public class ProductEmbeddingGenerator
{
    private readonly IProductRepository _productRepo;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ProductEmbeddingGenerator> _logger;

    public async Task GenerateAllEmbeddingsAsync(CancellationToken ct = default)
    {
        var products = await _productRepo.GetAllAsync(ct);
        var productsWithoutEmbedding = products.Where(p => p.Embedding == null).ToList();

        _logger.LogInformation(
            "Generating embeddings for {Count} products",
            productsWithoutEmbedding.Count);

        // Build embedding text
        var embeddingTexts = productsWithoutEmbedding.Select(p =>
        {
            var metadata = JsonSerializer.Deserialize<CosmeticsMetadata>(p.MetadataJson);
            return BuildEmbeddingText(p, metadata);
        }).ToList();

        // Generate in batches
        var embeddings = await _embeddingService.GenerateBatchAsync(embeddingTexts, ct);

        // Update products
        for (int i = 0; i < productsWithoutEmbedding.Count; i++)
        {
            productsWithoutEmbedding[i].Embedding = embeddings[i];
        }

        await _productRepo.UpdateBatchAsync(productsWithoutEmbedding, ct);

        _logger.LogInformation("Embeddings generated successfully");
    }

    private string BuildEmbeddingText(Product product, CosmeticsMetadata metadata)
    {
        // Combine all relevant text for embedding
        return $@"
Product: {product.Name}
Description: {product.Description}
Ingredients: {string.Join(", ", metadata.Ingredients)}
Skin Types: {string.Join(", ", metadata.SkinTypes)}
Skin Concerns: {string.Join(", ", metadata.SkinConcerns)}
Texture: {metadata.Texture}
pH: {metadata.pH}
        ".Trim();
    }
}
```

### 6. Integrate with Product Search

```csharp
public class ProductSearchService
{
    private readonly IVectorSearchRepository _vectorRepo;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ProductSearchService> _logger;

    public async Task<List<Product>> SearchAsync(
        string query,
        SkinProfile? skinProfile = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateAsync(query, ct);

        // Search with or without skin profile
        if (skinProfile != null)
        {
            return await _vectorRepo.SearchBySkinProfileAsync(
                queryEmbedding, skinProfile, limit, ct);
        }
        else
        {
            return await _vectorRepo.SearchBySimilarityAsync(
                queryEmbedding, limit, threshold: 0.7, ct);
        }
    }
}
```

### 7. Register Services

```csharp
// Program.cs
builder.Services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<GeminiAuthHandler>()
.AddHttpMessageHandler<GeminiRetryHandler>();

builder.Services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();
builder.Services.AddScoped<ProductSearchService>();
builder.Services.AddScoped<ProductEmbeddingGenerator>();
```

### 8. Create Migration Script

```bash
cd src/MessengerWebhook
dotnet ef migrations add AddProductEmbeddings
dotnet ef database update
```

### 9. Generate Initial Embeddings

```csharp
// Startup or admin endpoint
public class Startup
{
    public async Task ConfigureAsync(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<ProductEmbeddingGenerator>();
        await generator.GenerateAllEmbeddingsAsync();
    }
}
```

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task EmbeddingService_ShouldGenerateValidEmbedding()
{
    // Arrange
    var text = "Kem dưỡng cho da khô";

    // Act
    var embedding = await _embeddingService.GenerateAsync(text);

    // Assert
    Assert.NotNull(embedding);
    Assert.Equal(768, embedding.Length);
    Assert.All(embedding, v => Assert.InRange(v, -1.0, 1.0));
}

[Fact]
public async Task VectorSearch_ShouldReturnSimilarProducts()
{
    // Arrange
    var query = "Kem dưỡng cho da dầu mụn";
    var queryEmbedding = await _embeddingService.GenerateAsync(query);

    // Act
    var results = await _vectorRepo.SearchBySimilarityAsync(queryEmbedding, limit: 5);

    // Assert
    Assert.NotEmpty(results);
    Assert.All(results, p =>
    {
        var metadata = JsonSerializer.Deserialize<CosmeticsMetadata>(p.MetadataJson);
        Assert.Contains("oily", metadata.SkinTypes, StringComparer.OrdinalIgnoreCase);
    });
}
```

### Integration Tests

```csharp
[Fact]
public async Task ProductSearch_ShouldMatchIngredients()
{
    // Arrange
    var query = "Sản phẩm có niacinamide cho da dầu";
    var skinProfile = new SkinProfile { SkinType = "oily" };

    // Act
    var results = await _searchService.SearchAsync(query, skinProfile, limit: 5);

    // Assert
    Assert.NotEmpty(results);
    Assert.All(results, p =>
    {
        var metadata = JsonSerializer.Deserialize<CosmeticsMetadata>(p.MetadataJson);
        Assert.Contains("niacinamide", metadata.Ingredients, StringComparer.OrdinalIgnoreCase);
    });
}
```

---

## Performance Optimization

### 1. Index Tuning

```sql
-- Adjust lists parameter based on dataset size
-- Rule of thumb: lists = sqrt(total_rows)
CREATE INDEX idx_products_embedding
ON products USING ivfflat (embedding vector_cosine_ops)
WITH (lists = 100);  -- For ~10K products

-- For larger datasets (100K+)
WITH (lists = 316);  -- sqrt(100000)
```

### 2. Caching Strategy

```csharp
public class CachedEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly IDistributedCache _cache;

    public async Task<float[]> GenerateAsync(string text, CancellationToken ct)
    {
        var cacheKey = $"embedding:{ComputeHash(text)}";
        var cached = await _cache.GetAsync(cacheKey, ct);

        if (cached != null)
            return DeserializeEmbedding(cached);

        var embedding = await _inner.GenerateAsync(text, ct);
        await _cache.SetAsync(cacheKey, SerializeEmbedding(embedding),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            }, ct);

        return embedding;
    }
}
```

### 3. Batch Processing

```csharp
// Generate embeddings in background job
public class EmbeddingBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var productsWithoutEmbedding = await _productRepo
                .GetProductsWithoutEmbeddingAsync(limit: 100, ct);

            if (productsWithoutEmbedding.Any())
            {
                await _generator.GenerateEmbeddingsAsync(productsWithoutEmbedding, ct);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

---

## Cost Analysis

**Embedding API Pricing** (text-embedding-004):
- Free tier: 1,500 requests/day
- Paid: $0.00001 per 1K characters

**Scenario**: 10,000 products, avg 500 chars each
- Initial generation: 10K × 500 chars = 5M chars = $0.05
- Daily updates: 100 products × 500 chars = 50K chars = $0.0005/day

**Total cost**: ~$0.05 one-time + $0.15/month ongoing

---

## Success Criteria

- ✅ pgvector extension installed
- ✅ Embedding column added to products table
- ✅ All products have embeddings generated
- ✅ Vector search returns relevant results (>85% accuracy)
- ✅ Search latency <200ms (p95)
- ✅ Unit tests pass (100% coverage)
- ✅ Integration tests validate ingredient matching

---

## Next Steps

After Phase 2.5 completion:
1. Proceed to Phase 3: State Machine (use RAG in product search)
2. Proceed to Phase 4: Product Catalog (semantic search UI)
3. Proceed to Phase 5: Conversation Flows (skin profile → RAG search)
