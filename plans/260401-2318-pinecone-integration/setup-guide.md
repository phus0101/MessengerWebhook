# Pinecone Vector Search Setup Guide

## Overview

This guide walks through setting up Pinecone for product vector search in the MessengerWebhook application. The system uses a dual-storage strategy with both Pinecone (cloud) and pgvector (local) for resilience.

## Prerequisites

1. **Pinecone Account**
   - Sign up at [https://www.pinecone.io/](https://www.pinecone.io/)
   - Free tier available (sufficient for development)

2. **API Key**
   - Navigate to API Keys section in Pinecone console
   - Create a new API key
   - Copy the key (you won't be able to see it again)

3. **Development Environment**
   - .NET 8.0 SDK
   - PostgreSQL with pgvector extension
   - Access to Google Cloud Vertex AI (for embeddings)

## Environment Setup

### 1. Configure Environment Variables

Add to your `.env` file (development):

```env
# Pinecone Configuration
PINECONE_API_KEY=your-api-key-here
```

For production, use User Secrets or environment variables:

```bash
dotnet user-secrets set "Pinecone:ApiKey" "your-api-key-here"
```

### 2. Configure appsettings.json

Add Pinecone configuration section:

```json
{
  "Pinecone": {
    "ApiKey": "",
    "Environment": "us-east-1",
    "IndexName": "messenger-products",
    "TimeoutSeconds": 10
  }
}
```

**Configuration Options:**
- `ApiKey`: Your Pinecone API key (loaded from environment)
- `Environment`: Pinecone region (default: us-east-1)
- `IndexName`: Name of your Pinecone index
- `TimeoutSeconds`: HTTP timeout for Pinecone operations

## Index Creation

### 1. Create Index via Pinecone Console

Navigate to Pinecone console and create a new index with these specifications:

**Index Configuration:**
- **Name**: `messenger-products` (or match your `IndexName` config)
- **Dimensions**: `768` (matches Vertex AI text-embedding-004 model)
- **Metric**: `cosine` (for semantic similarity)
- **Pod Type**: `s1.x1` (starter pod, sufficient for development)
- **Replicas**: `1` (increase for production)
- **Pods**: `1` (scale based on data volume)

### 2. Create Index via Pinecone CLI (Alternative)

```bash
# Install Pinecone CLI
pip install pinecone-client

# Create index
pinecone create-index \
  --name messenger-products \
  --dimension 768 \
  --metric cosine \
  --pod-type s1.x1
```

### 3. Verify Index Creation

```bash
pinecone list-indexes
```

Expected output should show your index with status "Ready".

## Service Registration

The application automatically registers Pinecone services in `Program.cs`:

```csharp
// Pinecone client (singleton, thread-safe)
builder.Services.AddSingleton<PineconeClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException(
            "Pinecone:ApiKey is required. Set PINECONE_API_KEY in .env or User Secrets.");
    }

    return new PineconeClient(options.ApiKey);
});

// Vector search service (scoped)
builder.Services.AddScoped<IVectorSearchService, PineconeVectorService>();
builder.Services.AddScoped<ProductEmbeddingPipeline>();
```

## Multi-Tenant Namespace Pattern

### Namespace Strategy

The system uses Pinecone namespaces for tenant isolation:

- **Namespace Format**: `{tenantId}` (e.g., "tenant-123", "default")
- **Vector ID Format**: `{tenantId}-{productId}` (e.g., "tenant-123-prod-456")
- **Isolation**: Each tenant's vectors are stored in separate namespaces

### Implementation

```csharp
private string GetTenantNamespace()
{
    return _tenantContext.TenantId?.ToString() ?? "default";
}

// Usage in upsert
var vectorId = $"{tenantId}-{productId}";
await index.UpsertAsync(new UpsertRequest
{
    Vectors = new[] { vector },
    Namespace = tenantId  // Tenant isolation
});
```

### Benefits

- **Data Isolation**: Tenants cannot access each other's data
- **Query Scoping**: Searches automatically scoped to tenant namespace
- **Scalability**: Independent scaling per tenant
- **Deletion**: Easy tenant data cleanup

## Testing Instructions

### 1. Verify Configuration

Start the application and check logs for Pinecone initialization:

```bash
dotnet run
```

Expected log output:
```
[INF] Pinecone client initialized successfully
```

If you see an error about missing API key, verify your `.env` file or User Secrets.

### 2. Index a Single Product

Use the internal operations endpoint:

```bash
curl -X POST http://localhost:5000/internal/products/{productId}/index \
  -H "Content-Type: application/json"
```

Expected response:
```json
{
  "success": true,
  "message": "Product indexed successfully"
}
```

Check logs for confirmation:
```
[INF] Indexed product {ProductId}: {Name} to both pgvector and Pinecone
```

### 3. Batch Index All Products

```bash
curl -X POST http://localhost:5000/internal/products/index-all \
  -H "Content-Type: application/json"
```

Monitor logs for batch progress:
```
[INF] Starting batch indexing for 150 products
[INF] Indexed batch 10/150 to both pgvector and Pinecone
[INF] Indexed batch 20/150 to both pgvector and Pinecone
...
[INF] Batch indexing complete
```

### 4. Test Vector Search

Search for similar products:

```bash
curl -X POST http://localhost:5000/internal/products/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "kem dưỡng ẩm cho da khô",
    "topK": 5
  }'
```

Expected response:
```json
{
  "results": [
    {
      "productId": "prod-123",
      "name": "Kem Dưỡng Ẩm Intensive",
      "category": "Skincare",
      "price": 450000,
      "score": 0.89
    }
  ]
}
```

### 5. Verify Pinecone Console

Navigate to Pinecone console → Your Index → Vectors:
- Check vector count matches your product count
- Verify namespaces are created per tenant
- Inspect sample vectors and metadata

## Dual Storage Strategy

The system uses both Pinecone and pgvector with graceful degradation:

### Storage Flow

1. **Embedding Generation**: Vertex AI creates 768-dim vectors
2. **Primary Storage**: Save to PostgreSQL (pgvector) first
3. **Secondary Storage**: Upsert to Pinecone (with error handling)
4. **Failure Handling**: If Pinecone fails, operation still succeeds

### Code Example

```csharp
// Save to pgvector (critical path)
await _dbContext.SaveChangesAsync(cancellationToken);

// Upsert to Pinecone (best-effort)
try
{
    await _vectorSearch.UpsertProductAsync(
        productId, embedding, metadata, cancellationToken);
    _logger.LogInformation("Indexed to both pgvector and Pinecone");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Pinecone upsert failed. pgvector succeeded.");
    // Don't throw - dual storage strategy
}
```

### Benefits

- **Resilience**: System works even if Pinecone is down
- **Performance**: Local pgvector for low-latency queries
- **Scalability**: Pinecone for distributed search
- **Cost Optimization**: Use pgvector for dev, Pinecone for production

## Troubleshooting

### Issue: "Pinecone:ApiKey is required"

**Cause**: API key not configured

**Solution**:
1. Verify `.env` file contains `PINECONE_API_KEY=your-key`
2. For production, check User Secrets: `dotnet user-secrets list`
3. Restart application after adding key

### Issue: "Index not found"

**Cause**: Index name mismatch or index not created

**Solution**:
1. Verify index exists: `pinecone list-indexes`
2. Check `appsettings.json` → `Pinecone:IndexName` matches actual index
3. Create index if missing (see Index Creation section)

### Issue: "Dimension mismatch"

**Cause**: Index dimensions don't match embedding model

**Solution**:
1. Verify index dimensions: Check Pinecone console
2. Ensure index uses 768 dimensions (Vertex AI text-embedding-004)
3. Delete and recreate index with correct dimensions if needed

### Issue: "Upsert timeout"

**Cause**: Network latency or large batch size

**Solution**:
1. Increase `Pinecone:TimeoutSeconds` in config
2. Reduce batch size (default: 100 vectors per batch)
3. Check network connectivity to Pinecone region

### Issue: "Namespace not found"

**Cause**: Tenant context not properly initialized

**Solution**:
1. Verify `TenantResolutionMiddleware` is registered
2. Check request headers contain tenant identifier
3. Ensure tenant exists in database

### Issue: "Metadata type error"

**Cause**: Unsupported metadata value type

**Solution**:
1. Pinecone supports: string, number, boolean, array
2. Convert complex types to supported types in `BuildMetadata()`
3. Check `ConvertToMetadata()` method for type conversions

### Issue: "Rate limit exceeded"

**Cause**: Too many requests to Pinecone API

**Solution**:
1. Implement exponential backoff (already in `GeminiRetryHandler`)
2. Reduce batch indexing frequency
3. Upgrade Pinecone plan for higher rate limits

## Performance Optimization

### Batch Operations

Always use batch operations for multiple products:

```csharp
// Good: Batch upsert (100 vectors per request)
await _vectorSearch.UpsertBatchAsync(products, cancellationToken);

// Bad: Individual upserts (N requests)
foreach (var product in products)
{
    await _vectorSearch.UpsertProductAsync(...);
}
```

### Metadata Filtering

Use metadata filters to reduce search scope:

```csharp
var filters = new Dictionary<string, object>
{
    ["category"] = "Skincare",
    ["price"] = new { "$lte" = 500000 }
};

var results = await _vectorSearch.SearchSimilarAsync(
    queryEmbedding, topK: 10, filters: filters);
```

### Caching Strategy

Consider caching frequently searched queries:

```csharp
// Cache search results for 5 minutes
var cacheKey = $"search:{queryHash}:{tenantId}";
var cached = await _cache.GetAsync<List<ProductSearchResult>>(cacheKey);

if (cached != null)
    return cached;

var results = await _vectorSearch.SearchSimilarAsync(...);
await _cache.SetAsync(cacheKey, results, TimeSpan.FromMinutes(5));
```

## Monitoring

### Key Metrics

Monitor these metrics in production:

1. **Upsert Success Rate**: Track Pinecone upsert failures
2. **Search Latency**: P50, P95, P99 latencies
3. **Vector Count**: Total vectors per namespace
4. **API Usage**: Requests per second, rate limit hits
5. **Error Rate**: Failed operations percentage

### Logging

The service logs all operations:

```
[INF] Upserted product {ProductId} to Pinecone namespace {Namespace}
[INF] Found {Count} similar products for query in namespace {Namespace}
[ERR] Failed to upsert product {ProductId} to Pinecone
[ERR] Failed to search similar products in Pinecone
```

### Health Checks

Add Pinecone health check (optional):

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<PineconeHealthCheck>("pinecone");
```

## Security Considerations

### API Key Management

- **Never commit** API keys to source control
- Use `.env` for development (gitignored)
- Use User Secrets or Azure Key Vault for production
- Rotate keys periodically

### Tenant Isolation

- Namespaces provide logical isolation
- Vector IDs include tenant prefix
- Queries automatically scoped to tenant namespace
- Verify tenant context before all operations

### Data Privacy

- Product metadata stored in Pinecone
- Avoid storing PII in metadata
- Use product IDs as references, not full customer data
- Implement data retention policies

## Migration Guide

### From pgvector-only to Dual Storage

1. **Verify Pinecone Setup**: Complete all setup steps above
2. **Backfill Existing Data**: Run batch indexing for all products
3. **Monitor Logs**: Check for Pinecone upsert errors
4. **Validate Search**: Compare pgvector and Pinecone results
5. **Enable Production**: Update configuration for production environment

### Rollback Plan

If issues occur:

1. Pinecone failures don't affect core functionality (graceful degradation)
2. System continues using pgvector for search
3. Fix Pinecone issues without downtime
4. Re-run batch indexing after fixes

## Reference Files

Implementation files:
- `Services/VectorSearch/PineconeVectorService.cs` - Core Pinecone integration
- `Services/VectorSearch/ProductEmbeddingPipeline.cs` - Indexing pipeline
- `Configuration/PineconeOptions.cs` - Configuration model
- `Program.cs` - Service registration (lines 239-255)

## Next Steps

1. Complete Pinecone setup following this guide
2. Test with sample products
3. Monitor performance and error rates
4. Optimize batch sizes and caching strategy
5. Plan production deployment with proper monitoring
