# Pinecone Vector Database Setup Guide

## Prerequisites

1. **Pinecone Account**
   - Sign up at https://www.pinecone.io/
   - Create a new project
   - Get API key from dashboard

2. **Environment**
   - .NET 8.0 SDK
   - PostgreSQL with pgvector extension
   - Vertex AI credentials (for embeddings)

## Index Configuration

Create Pinecone index with these settings:

```bash
# Via Pinecone Console or API
Name: messenger-bot-products
Dimensions: 768
Metric: cosine
Cloud: AWS (or GCP)
Region: us-east-1 (or closest to your app)
Plan: Serverless (recommended for multi-tenant)
```

**Important:** Index name must match `Pinecone:IndexName` in appsettings.json

## Environment Setup

### 1. Add to `.env` file

```bash
# Pinecone Configuration
PINECONE_API_KEY=your-api-key-here
```

**Security:** Never commit `.env` to git. Already in `.gitignore`.

### 2. Update `appsettings.json`

```json
{
  "Pinecone": {
    "IndexName": "messenger-bot-products"
  }
}
```

### 3. Verify Configuration

Run application startup validation:

```bash
dotnet run
```

Should see log:
```
info: MessengerWebhook.Program[0]
      Pinecone client initialized successfully
```

## Multi-Tenant Pattern

Pinecone isolation uses **namespaces**:

- Namespace = `TenantId` (Guid)
- Vector ID = `{tenantId}-{productId}`
- Metadata includes `tenant_id` field

Example:
```
Tenant: 123e4567-e89b-12d3-a456-426614174000
Product: PROD-001
Vector ID: 123e4567-e89b-12d3-a456-426614174000-PROD-001
Namespace: 123e4567-e89b-12d3-a456-426614174000
```

## Testing

### 1. Run Integration Tests

```bash
cd tests/MessengerWebhook.IntegrationTests
dotnet test
```

All 99 tests should pass.

### 2. Manual Test

```csharp
// Index a product
await _pipeline.IndexProductAsync("PROD-001");

// Search
var embedding = await _embeddingService.EmbedAsync("kem dưỡng da");
var results = await _vectorSearch.SearchSimilarAsync(embedding, topK: 5);
```

## Dual Storage Strategy

System uses **pgvector + Pinecone**:

- **pgvector**: Primary storage, always succeeds
- **Pinecone**: Search optimization, graceful degradation

If Pinecone fails:
- Product still indexed in pgvector
- Error logged, operation continues
- Search falls back to pgvector

## Troubleshooting

### Error: "Pinecone:ApiKey is required"

**Cause:** API key not set in .env or User Secrets

**Fix:**
```bash
# Option 1: .env file
echo "PINECONE_API_KEY=your-key" >> .env

# Option 2: User Secrets (development)
dotnet user-secrets set "Pinecone:ApiKey" "your-key"
```

### Error: "Index not found"

**Cause:** Index name mismatch or not created

**Fix:**
1. Check index exists in Pinecone console
2. Verify `Pinecone:IndexName` matches exactly
3. Wait 1-2 minutes after index creation

### Error: "Dimension mismatch"

**Cause:** Index dimensions ≠ 768

**Fix:**
- Delete and recreate index with 768 dimensions
- Or create new index with correct dimensions

### Slow Search Performance

**Cause:** Cold start or large namespace

**Fix:**
- Pinecone Serverless has ~100ms cold start
- Consider Pinecone Pods for consistent latency
- Use batch operations for bulk indexing

### Integration Tests Fail

**Cause:** EF Core InMemory provider incompatibility

**Fix:** Already fixed in `MessengerBotDbContext.cs` with value converter:
```csharp
entity.Property(e => e.Embedding)
    .HasColumnType("vector(768)")
    .HasConversion(
        v => v.ToArray(),
        v => new Vector(v),
        new ValueComparer<Vector>(...)
    );
```

## Performance Benchmarks

**Upsert:**
- Single: ~50-100ms
- Batch (100): ~200-300ms

**Search:**
- Cold start: ~100-150ms
- Warm: ~30-50ms
- TopK=10: ~40ms

**Recommendations:**
- Use batch operations for bulk indexing
- Cache frequent queries
- Monitor via Pinecone dashboard

## Next Steps

1. Index existing products: `await _pipeline.IndexAllProductsAsync()`
2. Monitor Pinecone dashboard for usage
3. Set up alerts for API errors
4. Consider Pinecone Pods for production (if consistent latency needed)

## References

- Pinecone docs: https://docs.pinecone.io/
- Pinecone.Client SDK: https://github.com/pinecone-io/pinecone-dotnet-client
- pgvector: https://github.com/pgvector/pgvector
