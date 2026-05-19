# Phase 1: Vertex AI Setup

**Duration**: Week 1 (3-5 days)
**Priority**: P1 (Blocker for all other phases)
**Status**: ✅ COMPLETED (2026-04-01)

## Overview

Set up Google Cloud Vertex AI for text-embedding-004 model with service account authentication. Create embedding service that integrates with existing GeminiService architecture.

**Deliverable**: Working VertexAIEmbeddingService that generates 768-dim embeddings for Vietnamese text with <200ms latency.

## Context Links

- [RAG Architecture Research](../reports/researcher-260401-1113-rag-architecture-research.md)
- [Vietnamese Benchmark Results](../reports/researcher-260401-1311-vietnamese-embedding-benchmark.md)
- [Gemini Embedding 2 Research](../reports/researcher-260401-1259-gemini-embedding-2-rag.md)

## Key Insights

**Why text-embedding-004**:
- 100% accuracy on Vietnamese cosmetics benchmark (13/13 queries)
- GA status (stable, no breaking changes)
- Task optimization via `task_type=RETRIEVAL_DOCUMENT` improves recall
- 768 dimensions (sufficient for semantic search, lower storage cost)
- Bundled Vertex AI pricing (cost-effective vs per-token models)

**Latency Optimization**:
- Batch API: reduce 686ms → ~200ms
- Pre-compute product embeddings offline (one-time cost)
- Cache embeddings in Redis (Phase 4)

## Requirements

### Functional
- Generate embeddings for Vietnamese product descriptions
- Support batch embedding (up to 100 texts per request)
- Task-type optimization for retrieval use case
- Handle diacritics correctly (tested: "kem chong nang" → "Kem chống nắng")

### Non-Functional
- Latency: <200ms for single embedding, <500ms for batch (10 products)
- Availability: 99.9% (Vertex AI SLA)
- Cost: <$0.25/month for 10K embeddings
- Security: Service account with least-privilege IAM roles

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────┐
│           VertexAIEmbeddingService                  │
│  ┌───────────────────────────────────────────────┐  │
│  │  GenerateEmbeddingAsync(text)                 │  │
│  │  GenerateBatchEmbeddingsAsync(texts[])        │  │
│  └───────────────────────────────────────────────┘  │
│                      ↓                               │
│  ┌───────────────────────────────────────────────┐  │
│  │  HttpClient (with GoogleCredential auth)      │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Vertex AI Text Embeddings API                      │
│  POST /v1/projects/{project}/locations/{region}/    │
│       publishers/google/models/text-embedding-004:  │
│       predict                                       │
└─────────────────────────────────────────────────────┘
```

### Data Flow

```
Input: "Kem chống nắng cho da dầu"
    ↓
[VertexAIEmbeddingService]
    ↓
Build Request:
{
  "instances": [{
    "content": "Kem chống nắng cho da dầu",
    "task_type": "RETRIEVAL_DOCUMENT"
  }]
}
    ↓
[Vertex AI API] (asia-southeast1)
    ↓
Response:
{
  "predictions": [{
    "embeddings": {
      "values": [0.123, -0.456, ...] // 768 floats
    }
  }]
}
    ↓
Output: float[768]
```

## Related Code Files

### Files to Create

1. **Services/AI/Embeddings/VertexAIEmbeddingService.cs**
   - Implements `IEmbeddingService` interface
   - Handles authentication via service account
   - Batch embedding support
   - Retry logic with exponential backoff

2. **Services/AI/Embeddings/IEmbeddingService.cs**
   - Interface for embedding generation
   - Supports future embedding model swaps

3. **Configuration/VertexAIOptions.cs**
   - Configuration model for Vertex AI settings
   - Project ID, region, model name, timeout

4. **Models/Embedding.cs**
   - Value object for embedding vector
   - Cosine similarity helper methods

### Files to Modify

1. **Program.cs**
   - Register `IEmbeddingService` in DI container
   - Configure `VertexAIOptions` from appsettings
   - Add Google.Cloud.AIPlatform.V1 NuGet package

2. **appsettings.json**
   - Add VertexAI configuration section
   - Service account key path
   - Project ID and region

3. **MessengerWebhook.csproj**
   - Add NuGet: `Google.Cloud.AIPlatform.V1` (latest)
   - Add NuGet: `Google.Apis.Auth` (for service account)

## Implementation Steps

### Step 1: Google Cloud Setup (1 day)

**1.1 Create Google Cloud Project**
```bash
# Install gcloud CLI (if not installed)
# https://cloud.google.com/sdk/docs/install

# Login
gcloud auth login

# Create project
gcloud projects create messenger-rag-prod --name="Messenger RAG Production"

# Set as active project
gcloud config set project messenger-rag-prod

# Enable billing (required for Vertex AI)
# https://console.cloud.google.com/billing
```

**1.2 Enable Vertex AI API**
```bash
# Enable required APIs
gcloud services enable aiplatform.googleapis.com
gcloud services enable compute.googleapis.com

# Verify enabled
gcloud services list --enabled | grep aiplatform
```

**1.3 Create Service Account**
```bash
# Create service account
gcloud iam service-accounts create vertex-ai-embeddings \
    --display-name="Vertex AI Embeddings Service Account" \
    --description="Service account for generating embeddings via Vertex AI"

# Grant Vertex AI User role (least privilege)
gcloud projects add-iam-policy-binding messenger-rag-prod \
    --member="serviceAccount:vertex-ai-embeddings@messenger-rag-prod.iam.gserviceaccount.com" \
    --role="roles/aiplatform.user"

# Create and download key
gcloud iam service-accounts keys create vertex-ai-key.json \
    --iam-account=vertex-ai-embeddings@messenger-rag-prod.iam.gserviceaccount.com

# Store key securely (DO NOT commit to git)
# Move to: D:/secrets/vertex-ai-key.json
```

**1.4 Choose Region**
```bash
# Test latency from Vietnam to different regions
# asia-southeast1 (Singapore) - closest to Vietnam
# asia-east1 (Taiwan) - alternative

# Recommended: asia-southeast1 (Singapore)
```

### Step 2: Create Embedding Service (2 days)

**2.1 Create IEmbeddingService Interface**

```csharp
// Services/AI/Embeddings/IEmbeddingService.cs
namespace MessengerWebhook.Services.AI.Embeddings;

public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding for single text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default);
}
```

**2.2 Create VertexAIOptions Configuration**

```csharp
// Configuration/VertexAIOptions.cs
namespace MessengerWebhook.Configuration;

public class VertexAIOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Region { get; set; } = "asia-southeast1";
    public string ModelName { get; set; } = "text-embedding-004";
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
```

**2.3 Implement VertexAIEmbeddingService**

```csharp
// Services/AI/Embeddings/VertexAIEmbeddingService.cs
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.AI.Embeddings;

public class VertexAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly VertexAIOptions _options;
    private readonly ILogger<VertexAIEmbeddingService> _logger;
    private readonly string _endpoint;

    public VertexAIEmbeddingService(
        HttpClient httpClient,
        IOptions<VertexAIOptions> options,
        ILogger<VertexAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Build endpoint URL
        _endpoint = $"https://{_options.Region}-aiplatform.googleapis.com/v1/" +
                   $"projects/{_options.ProjectId}/locations/{_options.Region}/" +
                   $"publishers/google/models/{_options.ModelName}:predict";

        // Configure authentication
        ConfigureAuthentication();
    }

    private void ConfigureAuthentication()
    {
        var credential = GoogleCredential.FromFile(_options.ServiceAccountKeyPath)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

        var accessToken = credential.UnderlyingCredential
            .GetAccessTokenForRequestAsync().Result;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateBatchEmbeddingsAsync(
            new List<string> { text },
            cancellationToken);

        return embeddings[0];
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count > 100)
        {
            throw new ArgumentException(
                "Vertex AI supports max 100 texts per batch",
                nameof(texts));
        }

        var request = new
        {
            instances = texts.Select(text => new
            {
                content = text,
                task_type = "RETRIEVAL_DOCUMENT"
            }).ToArray()
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await _httpClient.PostAsJsonAsync(
            _endpoint,
            request,
            cancellationToken);

        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Vertex AI API error: {StatusCode} - {Error}",
                response.StatusCode,
                error);
            throw new HttpRequestException(
                $"Vertex AI API error: {response.StatusCode}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<VertexAIResponse>(cancellationToken);

        _logger.LogInformation(
            "Generated {Count} embeddings in {Ms}ms",
            texts.Count,
            stopwatch.ElapsedMilliseconds);

        return result!.Predictions
            .Select(p => p.Embeddings.Values)
            .ToList();
    }

    private class VertexAIResponse
    {
        public Prediction[] Predictions { get; set; } = Array.Empty<Prediction>();
    }

    private class Prediction
    {
        public EmbeddingData Embeddings { get; set; } = new();
    }

    private class EmbeddingData
    {
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
```

**2.4 Create Embedding Value Object**

```csharp
// Models/Embedding.cs
namespace MessengerWebhook.Models;

public record Embedding(float[] Values)
{
    public int Dimensions => Values.Length;

    /// <summary>
    /// Calculate cosine similarity with another embedding
    /// </summary>
    public double CosineSimilarity(Embedding other)
    {
        if (Dimensions != other.Dimensions)
        {
            throw new ArgumentException(
                "Embeddings must have same dimensions");
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < Dimensions; i++)
        {
            dotProduct += Values[i] * other.Values[i];
            normA += Values[i] * Values[i];
            normB += other.Values[i] * other.Values[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
```

### Step 3: Configuration & DI (1 day)

**3.1 Update appsettings.json**

```json
{
  "VertexAI": {
    "ProjectId": "messenger-rag-prod",
    "Region": "asia-southeast1",
    "ModelName": "text-embedding-004",
    "ServiceAccountKeyPath": "D:/secrets/vertex-ai-key.json",
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

**3.2 Update Program.cs**

```csharp
// Add VertexAI configuration
builder.Services.Configure<VertexAIOptions>(
    builder.Configuration.GetSection("VertexAI"));

// Register embedding service
builder.Services.AddHttpClient<IEmbeddingService, VertexAIEmbeddingService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });
```

**3.3 Update .csproj**

```xml
<ItemGroup>
  <PackageReference Include="Google.Cloud.AIPlatform.V1" Version="3.0.0" />
  <PackageReference Include="Google.Apis.Auth" Version="1.68.0" />
</ItemGroup>
```

### Step 4: Testing (1-2 days)

**4.1 Unit Tests**

```csharp
// tests/MessengerWebhook.UnitTests/Services/VertexAIEmbeddingServiceTests.cs
public class VertexAIEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_Returns768Dimensions()
    {
        // Arrange
        var service = CreateService();
        var text = "Kem chống nắng cho da dầu";

        // Act
        var embedding = await service.GenerateEmbeddingAsync(text);

        // Assert
        Assert.Equal(768, embedding.Length);
        Assert.All(embedding, value => Assert.InRange(value, -1.0f, 1.0f));
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_MultipleTexts_ReturnsCorrectCount()
    {
        // Arrange
        var service = CreateService();
        var texts = new List<string>
        {
            "Kem chống nắng",
            "Sữa rửa mặt",
            "Serum vitamin C"
        };

        // Act
        var embeddings = await service.GenerateBatchEmbeddingsAsync(texts);

        // Assert
        Assert.Equal(3, embeddings.Count);
        Assert.All(embeddings, emb => Assert.Equal(768, emb.Length));
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_Over100Texts_ThrowsException()
    {
        // Arrange
        var service = CreateService();
        var texts = Enumerable.Range(0, 101)
            .Select(i => $"Product {i}")
            .ToList();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateBatchEmbeddingsAsync(texts));
    }
}
```

**4.2 Integration Tests**

```csharp
// tests/MessengerWebhook.IntegrationTests/Services/VertexAIEmbeddingIntegrationTests.cs
public class VertexAIEmbeddingIntegrationTests
{
    [Fact]
    public async Task GenerateEmbedding_VietnameseText_HandlesCorrectly()
    {
        // Arrange
        var service = CreateRealService();
        var text = "Kem chống nắng vật lý Múi Xù";

        // Act
        var embedding = await service.GenerateEmbeddingAsync(text);

        // Assert
        Assert.Equal(768, embedding.Length);
        Assert.NotEqual(0, embedding.Sum()); // Non-zero vector
    }

    [Fact]
    public async Task GenerateEmbedding_DiacriticsVsNoDiacritics_AreSimilar()
    {
        // Arrange
        var service = CreateRealService();
        var withDiacritics = "Kem chống nắng";
        var withoutDiacritics = "Kem chong nang";

        // Act
        var emb1 = await service.GenerateEmbeddingAsync(withDiacritics);
        var emb2 = await service.GenerateEmbeddingAsync(withoutDiacritics);

        // Assert
        var similarity = CosineSimilarity(emb1, emb2);
        Assert.True(similarity > 0.6, $"Similarity {similarity} too low");
    }

    [Fact]
    public async Task GenerateBatchEmbeddings_Latency_UnderThreshold()
    {
        // Arrange
        var service = CreateRealService();
        var texts = Enumerable.Range(0, 10)
            .Select(i => $"Sản phẩm {i}")
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var embeddings = await service.GenerateBatchEmbeddingsAsync(texts);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Latency {stopwatch.ElapsedMilliseconds}ms exceeds 500ms threshold");
    }
}
```

**4.3 Vietnamese Benchmark Test**

```csharp
// tests/MessengerWebhook.IntegrationTests/Services/VietnameseBenchmarkTests.cs
public class VietnameseBenchmarkTests
{
    [Theory]
    [InlineData("kem chống nắng cho da dầu", "Kem chống nắng vật lý Múi Xù")]
    [InlineData("sữa rửa mặt cho da nhờn", "Sữa rửa mặt cho da dầu")]
    [InlineData("serum vitamin C", "Serum Vitamin C làm sáng da")]
    public async Task SemanticSearch_VietnameseQueries_FindsRelevantProducts(
        string query,
        string expectedProduct)
    {
        // Arrange
        var service = CreateRealService();
        var products = GetTestProducts();

        // Act
        var queryEmbedding = await service.GenerateEmbeddingAsync(query);
        var productEmbeddings = await service.GenerateBatchEmbeddingsAsync(
            products.Select(p => p.Description).ToList());

        // Calculate similarities
        var similarities = productEmbeddings
            .Select((emb, idx) => new
            {
                Product = products[idx],
                Similarity = CosineSimilarity(queryEmbedding, emb)
            })
            .OrderByDescending(x => x.Similarity)
            .ToList();

        // Assert
        var topResult = similarities.First();
        Assert.Contains(expectedProduct, topResult.Product.Name);
        Assert.True(topResult.Similarity > 0.6,
            $"Top similarity {topResult.Similarity} too low");
    }
}
```

## Success Criteria

### Functional
- [ ] Generate 768-dim embeddings for Vietnamese text
- [ ] Batch API supports up to 100 texts per request
- [ ] Handles diacritics correctly (similarity >0.6 with/without accents)
- [ ] Semantic search finds correct products (100% on 13-query benchmark)

### Performance
- [ ] Single embedding: <200ms (p95)
- [ ] Batch (10 products): <500ms (p95)
- [ ] Batch (100 products): <2s (p95)

### Quality
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Vietnamese benchmark: 100% accuracy (13/13 queries)

### Operational
- [ ] Service account has least-privilege IAM roles
- [ ] API key stored securely (not in git)
- [ ] Logging includes latency and error metrics
- [ ] Retry logic handles transient failures

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Service account key leaked | Low | Critical | Store in D:/secrets/, add to .gitignore, rotate quarterly |
| Vertex AI API rate limits | Low | Medium | Batch requests, implement exponential backoff |
| Latency >200ms | Medium | Medium | Use asia-southeast1 region, batch API, monitor p95 |
| Cost overrun | Low | Low | Set budget alerts at $10/month, monitor daily usage |
| Authentication failures | Low | High | Implement token refresh, retry with new token |

## Security Considerations

**Service Account**:
- Use least-privilege role: `roles/aiplatform.user` (not `roles/owner`)
- Store key outside git repository (D:/secrets/)
- Add to .gitignore: `**/secrets/**`, `**/*-key.json`
- Rotate key every 90 days

**API Security**:
- Use HTTPS only (enforced by Vertex AI)
- Validate input text length (<8192 tokens)
- Sanitize user input before embedding
- Rate limit embedding requests per user

**Secrets Management**:
```bash
# Production: Use Azure Key Vault
# Development: Use local secrets directory

# Add to .gitignore
echo "secrets/" >> .gitignore
echo "*-key.json" >> .gitignore
```

## Next Steps

After Phase 1 completion:
1. **Phase 2**: Create Pinecone vector database and index products
2. **Phase 3**: Implement hybrid search (vector + keyword)
3. **Phase 4**: Add Redis caching layer
4. **Phase 5**: Integrate RAG into GeminiService
5. **Phase 6**: Optimize and monitor production metrics

## Unresolved Questions

1. **Region Selection**: asia-southeast1 (Singapore) vs asia-east1 (Taiwan) - which has lower latency from Vietnam?
2. **Token Refresh**: How often does service account token expire? Need automatic refresh?
3. **Batch Size**: Is 100 texts per batch optimal, or should we use smaller batches (e.g., 50) for lower latency?
4. **Error Handling**: Should we fallback to gemini-embedding-001 (FREE tier) if Vertex AI fails?
5. **Monitoring**: What metrics should we track? (latency, error rate, cost, token usage)
