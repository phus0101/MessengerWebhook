# Phase 2: Vector Database Setup

**Duration**: Week 1-2 (5-7 days)
**Priority**: P1 (Blocker for Phase 3)
**Status**: ✅ COMPLETED (2026-04-01)
**Dependencies**: Phase 1 (Vertex AI embeddings) ✅

## Overview

Set up Pinecone Serverless vector database, create product embedding pipeline, and index 100 products with Vietnamese-optimized embeddings.

**Deliverable**: Indexed product catalog in Pinecone with <50ms query latency, supporting semantic search for Vietnamese queries.

## Context Links

- [Phase 1: Vertex AI Setup](phase-01-vertex-ai-setup.md)
- [RAG Architecture Research](../reports/researcher-260401-1113-rag-architecture-research.md)
- [Code Standards](../../docs/code-standards.md)

## Key Insights

**Why Pinecone**:
- Mature .NET SDK (actively maintained)
- Fully managed (zero ops overhead)
- Consistent <50ms query latency (99th percentile)
- Serverless pricing scales with usage ($0.096/1M queries)
- Production-proven for chatbots

**Index Strategy**:
- Namespace per tenant (multi-tenant isolation)
- Metadata: product_code, category, price, tenant_id
- 768 dimensions (text-embedding-004)
- Cosine similarity metric

## Requirements

### Functional
- Store 768-dim embeddings for 100+ products
- Query by vector similarity (top-K retrieval)
- Filter by metadata (category, price range, tenant)
- Multi-tenant isolation via namespaces
- Upsert/delete products on catalog updates

### Non-Functional
- Query latency: <50ms (p95)
- Index latency: <100ms per product
- Availability: 99.9% (Pinecone SLA)
- Cost: <$1/month for 100 products, 10K queries
- Scalability: Support 1000+ products per tenant

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────┐
│         PineconeVectorService                   │
│  ┌───────────────────────────────────────────┐  │
│  │  UpsertProductAsync(product, embedding)   │  │
│  │  SearchSimilarAsync(embedding, topK)      │  │
│  │  DeleteProductAsync(productId)            │  │
│  └───────────────────────────────────────────┘  │
│                    ↓                             │
│  ┌───────────────────────────────────────────┐  │
│  │  Pinecone .NET SDK                        │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│  Pinecone Serverless (us-east-1)                │
│  ┌───────────────────────────────────────────┐  │
│  │  Index: messenger-products                │  │
│  │  Dimensions: 768                          │  │
│  │  Metric: cosine                           │  │
│  │  Namespaces: tenant-{guid}                │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### Data Flow

```
Product Update Event
    ↓
[ProductEmbeddingPipeline]
    ↓
1. Load product from DB
2. Generate embedding (Vertex AI)
3. Upsert to Pinecone with metadata
    ↓
Pinecone Index:
{
  id: "prod-123",
  values: [0.123, -0.456, ...], // 768 floats
  metadata: {
    product_code: "MUI_XU_SPF50",
    name: "Kem chống nắng Múi Xù",
    category: "sunscreen",
    price: 250000,
    tenant_id: "guid"
  }
}
```

### Multi-Tenant Isolation

```
Pinecone Index: messenger-products
├─ Namespace: tenant-{guid-1}
│  ├─ prod-1 (768-dim vector + metadata)
│  ├─ prod-2
│  └─ prod-3
├─ Namespace: tenant-{guid-2}
│  ├─ prod-4
│  └─ prod-5
└─ Namespace: tenant-{guid-3}
   └─ prod-6
```

## Related Code Files

### Files to Create

1. **Services/VectorSearch/PineconeVectorService.cs**
   - Implements `IVectorSearchService`
   - Upsert, query, delete operations
   - Namespace management for multi-tenancy

2. **Services/VectorSearch/IVectorSearchService.cs**
   - Interface for vector search operations
   - Supports future vector DB swaps

3. **Services/VectorSearch/ProductEmbeddingPipeline.cs**
   - Orchestrates embedding generation + indexing
   - Batch processing for bulk indexing
   - Error handling and retry logic

4. **Data/Entities/ProductEmbedding.cs**
   - EF Core entity for storing embeddings locally
   - Backup for Pinecone data
   - Enables offline development

5. **Configuration/PineconeOptions.cs**
   - Configuration model for Pinecone settings
   - API key, environment, index name

6. **Data/Migrations/AddProductEmbedding.cs**
   - EF Core migration for ProductEmbedding table
   - Vector column (pgvector extension)

### Files to Modify

1. **Data/MessengerBotDbContext.cs**
   - Add `DbSet<ProductEmbedding>`
   - Configure vector column (pgvector)

2. **Program.cs**
   - Register `IVectorSearchService` in DI
   - Configure `PineconeOptions`
   - Add Pinecone NuGet package

3. **appsettings.json**
   - Add Pinecone configuration section
   - API key, environment, index name

4. **MessengerWebhook.csproj**
   - Add NuGet: `Pinecone.Client` (latest)
   - Add NuGet: `Npgsql.EntityFrameworkCore.PostgreSQL.Vector` (pgvector)

## Implementation Steps

### Step 1: Pinecone Account Setup (1 day)

**1.1 Create Pinecone Account**
```bash
# Sign up at https://www.pinecone.io/
# Choose Serverless plan (free tier: 100K vectors, 1M queries/month)
```

**1.2 Create Index**
```bash
# Via Pinecone Console:
# - Name: messenger-products
# - Dimensions: 768
# - Metric: cosine
# - Cloud: AWS
# - Region: us-east-1 (lowest latency to Vietnam via CloudFront)
# - Serverless: Yes
```

**1.3 Get API Key**
```bash
# Copy API key from Pinecone Console
# Store in: D:/secrets/pinecone-api-key.txt
```

### Step 2: Create Vector Search Service (2 days)

**2.1 Create IVectorSearchService Interface**

```csharp
// Services/VectorSearch/IVectorSearchService.cs
namespace MessengerWebhook.Services.VectorSearch;

public interface IVectorSearchService
{
    /// <summary>
    /// Upsert product embedding to vector index
    /// </summary>
    Task UpsertProductAsync(
        string productId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar products by embedding
    /// </summary>
    Task<List<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete product from vector index
    /// </summary>
    Task DeleteProductAsync(
        string productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch upsert multiple products
    /// </summary>
    Task UpsertBatchAsync(
        List<(string productId, float[] embedding, Dictionary<string, object> metadata)> batch,
        CancellationToken cancellationToken = default);
}

public record VectorSearchResult(
    string ProductId,
    double Score,
    Dictionary<string, object> Metadata);
```

**2.2 Create PineconeOptions Configuration**

```csharp
// Configuration/PineconeOptions.cs
namespace MessengerWebhook.Configuration;

public class PineconeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Environment { get; set; } = "us-east-1";
    public string IndexName { get; set; } = "messenger-products";
    public int TimeoutSeconds { get; set; } = 10;
}
```

**2.3 Implement PineconeVectorService**

```csharp
// Services/VectorSearch/PineconeVectorService.cs
using Pinecone;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.Tenant;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.VectorSearch;

public class PineconeVectorService : IVectorSearchService
{
    private readonly PineconeClient _client;
    private readonly PineconeOptions _options;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PineconeVectorService> _logger;

    public PineconeVectorService(
        IOptions<PineconeOptions> options,
        ITenantContext tenantContext,
        ILogger<PineconeVectorService> logger)
    {
        _options = options.Value;
        _tenantContext = tenantContext;
        _logger = logger;

        _client = new PineconeClient(_options.ApiKey);
    }

    public async Task UpsertProductAsync(
        string productId,
        float[] embedding,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_options.IndexName);
        var ns = GetNamespace();

        // Add tenant_id to metadata
        metadata["tenant_id"] = _tenantContext.TenantId.ToString();

        var vector = new Vector
        {
            Id = productId,
            Values = embedding,
            Metadata = metadata
        };

        await index.UpsertAsync(
            new[] { vector },
            ns,
            cancellationToken);

        _logger.LogInformation(
            "Upserted product {ProductId} to namespace {Namespace}",
            productId,
            ns);
    }

    public async Task<List<VectorSearchResult>> SearchSimilarAsync(
        float[] queryEmbedding,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_options.IndexName);
        var ns = GetNamespace();

        // Add tenant filter
        filter ??= new Dictionary<string, object>();
        filter["tenant_id"] = _tenantContext.TenantId.ToString();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await index.QueryAsync(
            new QueryRequest
            {
                Vector = queryEmbedding,
                TopK = topK,
                Namespace = ns,
                Filter = filter,
                IncludeMetadata = true
            },
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Vector search returned {Count} results in {Ms}ms",
            response.Matches.Count,
            stopwatch.ElapsedMilliseconds);

        return response.Matches
            .Select(m => new VectorSearchResult(
                m.Id,
                m.Score,
                m.Metadata))
            .ToList();
    }

    public async Task DeleteProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_options.IndexName);
        var ns = GetNamespace();

        await index.DeleteAsync(
            new[] { productId },
            ns,
            cancellationToken);

        _logger.LogInformation(
            "Deleted product {ProductId} from namespace {Namespace}",
            productId,
            ns);
    }

    public async Task UpsertBatchAsync(
        List<(string productId, float[] embedding, Dictionary<string, object> metadata)> batch,
        CancellationToken cancellationToken = default)
    {
        var index = _client.Index(_options.IndexName);
        var ns = GetNamespace();

        var vectors = batch.Select(item =>
        {
            item.metadata["tenant_id"] = _tenantContext.TenantId.ToString();
            return new Vector
            {
                Id = item.productId,
                Values = item.embedding,
                Metadata = item.metadata
            };
        }).ToArray();

        await index.UpsertAsync(vectors, ns, cancellationToken);

        _logger.LogInformation(
            "Batch upserted {Count} products to namespace {Namespace}",
            batch.Count,
            ns);
    }

    private string GetNamespace()
    {
        return $"tenant-{_tenantContext.TenantId}";
    }
}
```

### Step 3: Create Product Embedding Pipeline (2 days)

**3.1 Create ProductEmbedding Entity**

```csharp
// Data/Entities/ProductEmbedding.cs
using Pgvector;

namespace MessengerWebhook.Data.Entities;

public class ProductEmbedding : ITenantOwnedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!; // pgvector type
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
}
```

**3.2 Update DbContext**

```csharp
// Data/MessengerBotDbContext.cs
public DbSet<ProductEmbedding> ProductEmbeddings { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure ProductEmbedding
    modelBuilder.Entity<ProductEmbedding>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.TenantId, e.ProductId }).IsUnique();
        entity.HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

        entity.HasOne(e => e.Product)
            .WithOne()
            .HasForeignKey<ProductEmbedding>(e => e.ProductId);

        // pgvector column
        entity.Property(e => e.Embedding)
            .HasColumnType("vector(768)");
    });
}
```

**3.3 Create Migration**

```bash
dotnet ef migrations add AddProductEmbedding --project src/MessengerWebhook
```

**3.4 Create ProductEmbeddingPipeline**

```csharp
// Services/VectorSearch/ProductEmbeddingPipeline.cs
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.VectorSearch;

public class ProductEmbeddingPipeline
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly MessengerBotDbContext _dbContext;
    private readonly ILogger<ProductEmbeddingPipeline> _logger;

    public ProductEmbeddingPipeline(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        MessengerBotDbContext dbContext,
        ILogger<ProductEmbeddingPipeline> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Index single product
    /// </summary>
    public async Task IndexProductAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            throw new ArgumentException($"Product {productId} not found");
        }

        // Generate embedding
        var text = BuildProductText(product);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            text,
            cancellationToken);

        // Store in database
        var productEmbedding = await _dbContext.ProductEmbeddings
            .FirstOrDefaultAsync(
                e => e.ProductId == productId,
                cancellationToken);

        if (productEmbedding == null)
        {
            productEmbedding = new ProductEmbedding
            {
                Id = Guid.NewGuid(),
                TenantId = product.TenantId,
                ProductId = productId,
                Embedding = new Pgvector.Vector(embedding),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ProductEmbeddings.Add(productEmbedding);
        }
        else
        {
            productEmbedding.Embedding = new Pgvector.Vector(embedding);
            productEmbedding.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Upsert to Pinecone
        var metadata = BuildMetadata(product);
        await _vectorSearch.UpsertProductAsync(
            productId,
            embedding,
            metadata,
            cancellationToken);

        _logger.LogInformation(
            "Indexed product {ProductId}: {Name}",
            productId,
            product.Name);
    }

    /// <summary>
    /// Batch index all products for current tenant
    /// </summary>
    public async Task IndexAllProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _dbContext.Products.ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Starting batch indexing for {Count} products",
            products.Count);

        // Process in batches of 10
        var batchSize = 10;
        for (int i = 0; i < products.Count; i += batchSize)
        {
            var batch = products.Skip(i).Take(batchSize).ToList();

            // Generate embeddings in batch
            var texts = batch.Select(BuildProductText).ToList();
            var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(
                texts,
                cancellationToken);

            // Prepare batch for Pinecone
            var pineconeB atch = batch.Select((product, idx) => (
                productId: product.Id,
                embedding: embeddings[idx],
                metadata: BuildMetadata(product)
            )).ToList();

            // Upsert to Pinecone
            await _vectorSearch.UpsertBatchAsync(
                pineconeB atch,
                cancellationToken);

            // Store in database
            foreach (var (product, idx) in batch.Select((p, i) => (p, i)))
            {
                var productEmbedding = new ProductEmbedding
                {
                    Id = Guid.NewGuid(),
                    TenantId = product.TenantId,
                    ProductId = product.Id,
                    Embedding = new Pgvector.Vector(embeddings[idx]),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ProductEmbeddings.Add(productEmbedding);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Indexed batch {Current}/{Total}",
                Math.Min(i + batchSize, products.Count),
                products.Count);
        }

        _logger.LogInformation("Batch indexing complete");
    }

    private string BuildProductText(Product product)
    {
        // Combine product fields for embedding
        return $"{product.Name}. {product.Description}. " +
               $"Danh mục: {product.Category}. " +
               $"Giá: {product.Price:N0}đ";
    }

    private Dictionary<string, object> BuildMetadata(Product product)
    {
        return new Dictionary<string, object>
        {
            ["product_code"] = product.Code,
            ["name"] = product.Name,
            ["category"] = product.Category ?? "",
            ["price"] = product.Price,
            ["tenant_id"] = product.TenantId.ToString()
        };
    }
}
```

### Step 4: Configuration & DI (1 day)

**4.1 Update appsettings.json**

```json
{
  "Pinecone": {
    "ApiKey": "{{PINECONE_API_KEY}}",
    "Environment": "us-east-1",
    "IndexName": "messenger-products",
    "TimeoutSeconds": 10
  }
}
```

**4.2 Update Program.cs**

```csharp
// Add Pinecone configuration
builder.Services.Configure<PineconeOptions>(
    builder.Configuration.GetSection("Pinecone"));

// Register vector search service
builder.Services.AddScoped<IVectorSearchService, PineconeVectorService>();
builder.Services.AddScoped<ProductEmbeddingPipeline>();

// Enable pgvector
builder.Services.AddDbContext<MessengerBotDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        o => o.UseVector()); // Enable pgvector
});
```

**4.3 Update .csproj**

```xml
<ItemGroup>
  <PackageReference Include="Pinecone.Client" Version="2.0.0" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL.Vector" Version="8.0.0" />
</ItemGroup>
```

### Step 5: Testing (1-2 days)

**5.1 Unit Tests**

```csharp
// tests/MessengerWebhook.UnitTests/Services/PineconeVectorServiceTests.cs
public class PineconeVectorServiceTests
{
    [Fact]
    public async Task UpsertProductAsync_ValidData_Succeeds()
    {
        // Arrange
        var service = CreateService();
        var productId = "prod-123";
        var embedding = Enumerable.Range(0, 768).Select(_ => 0.1f).ToArray();
        var metadata = new Dictionary<string, object>
        {
            ["product_code"] = "MUI_XU",
            ["name"] = "Kem chống nắng",
            ["category"] = "sunscreen",
            ["price"] = 250000
        };

        // Act
        await service.UpsertProductAsync(productId, embedding, metadata);

        // Assert - verify via search
        var results = await service.SearchSimilarAsync(embedding, topK: 1);
        Assert.Single(results);
        Assert.Equal(productId, results[0].ProductId);
    }

    [Fact]
    public async Task SearchSimilarAsync_MultiTenant_IsolatesResults()
    {
        // Arrange
        var service1 = CreateServiceForTenant(tenant1Id);
        var service2 = CreateServiceForTenant(tenant2Id);

        // Index products for both tenants
        await service1.UpsertProductAsync("prod-1", embedding1, metadata1);
        await service2.UpsertProductAsync("prod-2", embedding2, metadata2);

        // Act - search as tenant 1
        var results = await service1.SearchSimilarAsync(embedding1, topK: 10);

        // Assert - only see tenant 1 products
        Assert.Single(results);
        Assert.Equal("prod-1", results[0].ProductId);
    }
}
```

**5.2 Integration Tests**

```csharp
// tests/MessengerWebhook.IntegrationTests/Services/ProductEmbeddingPipelineTests.cs
public class ProductEmbeddingPipelineTests : IAsyncLifetime
{
    [Fact]
    public async Task IndexProductAsync_CreatesEmbeddingAndIndexes()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var product = await CreateTestProduct();

        // Act
        await pipeline.IndexProductAsync(product.Id);

        // Assert - check database
        var embedding = await _dbContext.ProductEmbeddings
            .FirstOrDefaultAsync(e => e.ProductId == product.Id);
        Assert.NotNull(embedding);
        Assert.Equal(768, embedding.Embedding.ToArray().Length);

        // Assert - check Pinecone
        var results = await _vectorSearch.SearchSimilarAsync(
            embedding.Embedding.ToArray(),
            topK: 1);
        Assert.Single(results);
        Assert.Equal(product.Id, results[0].ProductId);
    }

    [Fact]
    public async Task IndexAllProductsAsync_BatchProcesses()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var products = await CreateTestProducts(25); // 3 batches

        // Act
        var stopwatch = Stopwatch.StartNew();
        await pipeline.IndexAllProductsAsync();
        stopwatch.Stop();

        // Assert
        var embeddings = await _dbContext.ProductEmbeddings.ToListAsync();
        Assert.Equal(25, embeddings.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 10000,
            $"Batch indexing took {stopwatch.ElapsedMilliseconds}ms (expected <10s)");
    }
}
```

**5.3 Vietnamese Search Test**

```csharp
// tests/MessengerWebhook.IntegrationTests/Services/VietnameseVectorSearchTests.cs
public class VietnameseVectorSearchTests
{
    [Theory]
    [InlineData("kem chống nắng cho da dầu", "MUI_XU_SPF50")]
    [InlineData("sữa rửa mặt cho da nhờn", "CLEANSER_OILY")]
    [InlineData("serum vitamin C", "SERUM_VIT_C")]
    public async Task VectorSearch_VietnameseQueries_FindsCorrectProducts(
        string query,
        string expectedProductCode)
    {
        // Arrange
        await IndexTestProducts();
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // Act
        var results = await _vectorSearch.SearchSimilarAsync(
            queryEmbedding,
            topK: 3);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal(expectedProductCode, topResult.Metadata["product_code"]);
        Assert.True(topResult.Score > 0.6,
            $"Score {topResult.Score} too low for query: {query}");
    }
}
```

## Success Criteria

### Functional
- [ ] Index 100 products with 768-dim embeddings
- [ ] Query returns top-K similar products
- [ ] Multi-tenant isolation via namespaces
- [ ] Metadata filtering (category, price range)
- [ ] Batch upsert supports 10+ products

### Performance
- [ ] Query latency: <50ms (p95)
- [ ] Index latency: <100ms per product
- [ ] Batch index (100 products): <10s

### Quality
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Vietnamese search: 100% accuracy (13/13 queries)
- [ ] Multi-tenant isolation verified

### Operational
- [ ] Pinecone API key stored securely
- [ ] Database backup for embeddings (pgvector)
- [ ] Logging includes latency and error metrics
- [ ] Cost monitoring dashboard

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Pinecone API key leaked | Low | Critical | Store in secrets/, add to .gitignore, rotate quarterly |
| Query latency >50ms | Low | Medium | Use us-east-1 region, monitor p95, add timeout |
| Index out of sync with DB | Medium | Medium | Rebuild index on startup, add consistency checks |
| Cost overrun | Low | Low | Set budget alerts at $10/month, monitor daily |
| Namespace collision | Low | High | Use tenant GUID in namespace, validate uniqueness |

## Security Considerations

**API Key Management**:
- Store Pinecone API key in secrets/ directory
- Add to .gitignore: `**/secrets/**`, `**/*-api-key.txt`
- Rotate key every 90 days
- Use environment variables in production

**Multi-Tenant Isolation**:
- Namespace per tenant: `tenant-{guid}`
- Add tenant_id to all metadata
- Filter by tenant_id in queries
- Validate namespace access in service layer

**Data Privacy**:
- Product descriptions may contain sensitive info
- Embeddings are not reversible (safe to store)
- Metadata should not include PII
- Audit log for index operations

## Next Steps

After Phase 2 completion:
1. **Phase 3**: Implement hybrid search (vector + keyword BM25)
2. **Phase 4**: Add Redis caching layer
3. **Phase 5**: Integrate RAG into GeminiService
4. **Phase 6**: Optimize and monitor production metrics

## Unresolved Questions

1. **Index Rebuild**: How often should we rebuild the entire index? (weekly, monthly, on-demand)
2. **Consistency**: Should we validate Pinecone vs DB consistency on startup?
3. **Backup Strategy**: Should we export Pinecone index periodically for disaster recovery?
4. **Metadata Size**: What's the max metadata size per vector? (affects filtering performance)
5. **Namespace Limit**: How many namespaces can Pinecone support? (affects tenant scaling)
