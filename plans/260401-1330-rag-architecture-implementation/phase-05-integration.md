# Phase 5: RAG Integration

**Duration**: Week 4-5 (5-7 days)
**Priority**: P1 (Critical path)
**Status**: Pending
**Dependencies**: Phase 1-4 (All previous phases)

## Overview

Integrate RAG pipeline into GeminiService and ConversationStateMachine, replacing full-catalog prompts with dynamic retrieval. Includes fallback handling, feature flag for gradual rollout, and A/B testing infrastructure.

**Deliverable**: Production-ready RAG chatbot with 91.9% cost reduction, 75% latency improvement, and 100% Vietnamese accuracy.

## Context Links

- [Phase 4: Caching Layer](phase-04-caching-layer.md)
- [Root Cause Analysis](../reports/root-cause-260401-1036-customer-experience-issues.md)

## Key Insights

**Integration Points**:
- GeminiService: Add RAG context assembly before LLM call
- SalesStateHandlerBase: Replace hardcoded product list with RAG retrieval
- ConversationStateMachine: Add RAG service to DI
- Feature flag: Gradual rollout (10%→50%→100%)

**Backwards Compatibility**:
- RAG is additive (no schema changes)
- Fallback to full-context mode if RAG fails
- Feature flag allows instant rollback
- Existing conversations unaffected

## Requirements

### Functional
- Retrieve top-5 products via hybrid search for user queries
- Assemble context with product details (name, price, description)
- Pass context to Gemini with system prompt
- Handle RAG failures gracefully (fallback to full-context)
- Feature flag for gradual rollout

### Non-Functional
- End-to-end latency: <1s (p95)
- RAG retrieval: <200ms (p95)
- Accuracy: 100% on Vietnamese benchmark
- Availability: 99.5% (with fallback)
- Cost: <$60/month (91.9% reduction)

## Architecture

### RAG Pipeline Integration

```
User Message
    ↓
[ConversationStateMachine]
    ↓
[SalesStateHandlerBase]
    ↓
Extract Query Intent
    ↓
[RAGService.RetrieveContextAsync(query)]
    ↓
┌─────────────────────────────────────┐
│ Hybrid Search (cached)              │
│ ├─ Embedding (cached)               │
│ ├─ Vector + Keyword Search          │
│ └─ RRF Fusion                       │
└─────────────────────────────────────┘
    ↓
Top-5 Products
    ↓
[RAGService.AssembleContext(products)]
    ↓
Context String:
"""
Sản phẩm liên quan:
1. Kem chống nắng Múi Xù - 250,000đ
   Bảo vệ da khỏi tia UV...
2. Sữa rửa mặt cho da dầu - 180,000đ
   Làm sạch sâu...
...
"""
    ↓
[GeminiService.SendMessageAsync(context + query)]
    ↓
Response
```

### Fallback Strategy

```
RAG Pipeline
    ↓
Try: Hybrid Search
    ↓ (timeout or error)
Fallback 1: Vector-only search
    ↓ (timeout or error)
Fallback 2: Full-context mode (current approach)
    ↓
Log failure, alert monitoring
```

## Related Code Files

### Files to Create

1. **Services/RAG/RAGService.cs**
   - Orchestrates RAG pipeline
   - Retrieval + context assembly
   - Fallback handling

2. **Services/RAG/IRAGService.cs**
   - Interface for RAG operations
   - Supports future RAG strategy swaps

3. **Services/RAG/ContextAssembler.cs**
   - Formats products into LLM context
   - Vietnamese-friendly formatting
   - Token budget management

4. **Services/RAG/RAGMetricsCollector.cs**
   - Tracks RAG performance metrics
   - Latency, accuracy, cache hit rate
   - Cost tracking

5. **Configuration/RAGOptions.cs**
   - RAG configuration settings
   - TopK, fallback strategy, feature flag

### Files to Modify

1. **Services/AI/GeminiService.cs**
   - Add RAG context parameter
   - Integrate prompt caching
   - Track token usage

2. **StateMachine/Handlers/SalesStateHandlerBase.cs**
   - Replace hardcoded product list with RAG
   - Extract query from user message
   - Handle RAG failures

3. **StateMachine/ConversationStateMachine.cs**
   - Inject RAGService into handlers
   - Feature flag check

4. **Program.cs**
   - Register RAGService in DI
   - Configure RAGOptions

5. **appsettings.json**
   - Add RAG configuration section
   - Feature flag settings

## Implementation Steps

### Step 1: Create RAG Service (2 days)

**1.1 Create IRAGService Interface**

```csharp
// Services/RAG/IRAGService.cs
namespace MessengerWebhook.Services.RAG;

public interface IRAGService
{
    /// <summary>
    /// Retrieve relevant products and assemble context for LLM
    /// </summary>
    Task<RAGContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

public record RAGContext(
    string FormattedContext,
    List<string> ProductIds,
    RAGMetrics Metrics);

public record RAGMetrics(
    TimeSpan RetrievalLatency,
    TimeSpan TotalLatency,
    int ProductsRetrieved,
    bool CacheHit,
    string Source); // "hybrid", "vector-only", "fallback"
```

**1.2 Create ContextAssembler**

```csharp
// Services/RAG/ContextAssembler.cs
namespace MessengerWebhook.Services.RAG;

public class ContextAssembler
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ILogger<ContextAssembler> _logger;

    public ContextAssembler(
        MessengerBotDbContext dbContext,
        ILogger<ContextAssembler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> AssembleContextAsync(
        List<string> productIds,
        CancellationToken cancellationToken = default)
    {
        // Load products from database
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Sort by original order
        var sortedProducts = productIds
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null)
            .ToList();

        // Format context
        var context = new StringBuilder();
        context.AppendLine("Sản phẩm liên quan:");
        context.AppendLine();

        for (int i = 0; i < sortedProducts.Count; i++)
        {
            var product = sortedProducts[i];
            context.AppendLine($"{i + 1}. {product.Name} - {product.Price:N0}đ");
            context.AppendLine($"   Mã: {product.Code}");
            context.AppendLine($"   {product.Description}");
            context.AppendLine();
        }

        _logger.LogInformation(
            "Assembled context for {Count} products, {Tokens} tokens",
            sortedProducts.Count,
            EstimateTokens(context.ToString()));

        return context.ToString();
    }

    private int EstimateTokens(string text)
    {
        // Rough estimate: 1 token ≈ 4 characters for Vietnamese
        return text.Length / 4;
    }
}
```

**1.3 Implement RAGService**

```csharp
// Services/RAG/RAGService.cs
using MessengerWebhook.Services.VectorSearch;

namespace MessengerWebhook.Services.RAG;

public class RAGService : IRAGService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly ContextAssembler _contextAssembler;
    private readonly RAGOptions _options;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IHybridSearchService hybridSearch,
        ContextAssembler contextAssembler,
        IOptions<RAGOptions> options,
        ILogger<RAGService> logger)
    {
        _hybridSearch = hybridSearch;
        _contextAssembler = contextAssembler;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RAGContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var retrievalStopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve products via hybrid search
            var results = await _hybridSearch.SearchAsync(
                query,
                topK,
                filter: null,
                cancellationToken);

            retrievalStopwatch.Stop();

            if (results.Count == 0)
            {
                _logger.LogWarning("No products found for query: {Query}", query);
                return CreateEmptyContext(stopwatch.Elapsed);
            }

            // Extract product IDs
            var productIds = results.Select(r => r.ProductId).ToList();

            // Assemble context
            var context = await _contextAssembler.AssembleContextAsync(
                productIds,
                cancellationToken);

            stopwatch.Stop();

            var metrics = new RAGMetrics(
                retrievalStopwatch.Elapsed,
                stopwatch.Elapsed,
                results.Count,
                CacheHit: false, // TODO: detect from search service
                Source: "hybrid");

            _logger.LogInformation(
                "RAG retrieval: {Query} → {Count} products in {Ms}ms",
                query,
                results.Count,
                stopwatch.ElapsedMilliseconds);

            return new RAGContext(context, productIds, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed for query: {Query}", query);

            // Fallback to empty context
            return CreateEmptyContext(stopwatch.Elapsed);
        }
    }

    private RAGContext CreateEmptyContext(TimeSpan elapsed)
    {
        return new RAGContext(
            "Không tìm thấy sản phẩm phù hợp.",
            new List<string>(),
            new RAGMetrics(elapsed, elapsed, 0, false, "fallback"));
    }
}
```

### Step 2: Integrate into GeminiService (1 day)

**2.1 Update GeminiService**

```csharp
// Services/AI/GeminiService.cs (modifications)
public async Task<string> SendMessageAsync(
    string userId,
    string message,
    List<ConversationMessage> history,
    string? ragContext = null, // NEW: RAG context
    GeminiModelType? modelOverride = null,
    CancellationToken cancellationToken = default)
{
    // ... existing validation ...

    // Build prompt with RAG context
    var prompt = ragContext != null
        ? $"{ragContext}\n\nKhách hỏi: {message}"
        : message;

    // Build request with prompt caching
    var request = new
    {
        contents = BuildContents(prompt, history),
        generationConfig = new
        {
            temperature = _options.Temperature,
            maxOutputTokens = _options.MaxTokens
        },
        cachedContent = new
        {
            model = modelName,
            systemInstruction = _systemPrompt,
            contents = BuildHistoryForCache(history),
            ttl = "300s"
        }
    };

    // ... rest of implementation ...
}
```

### Step 3: Integrate into SalesStateHandlerBase (2 days)

**3.1 Update SalesStateHandlerBase**

```csharp
// StateMachine/Handlers/SalesStateHandlerBase.cs (modifications)
public class SalesStateHandlerBase : BaseStateHandler
{
    protected readonly IRAGService _ragService;
    protected readonly RAGOptions _ragOptions;

    public SalesStateHandlerBase(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        IRAGService ragService,
        IOptions<RAGOptions> ragOptions,
        ILogger<SalesStateHandlerBase> logger)
        : base(geminiService, stateMachine, logger)
    {
        _ragService = ragService;
        _ragOptions = ragOptions.Value;
    }

    protected override async Task<string> HandleInternalAsync(
        StateContext ctx,
        string message)
    {
        // Extract query intent
        var query = ExtractProductQuery(message);

        // Retrieve RAG context if enabled
        string? ragContext = null;
        if (_ragOptions.Enabled && !string.IsNullOrEmpty(query))
        {
            try
            {
                var ragResult = await _ragService.RetrieveContextAsync(
                    query,
                    topK: _ragOptions.TopK);

                ragContext = ragResult.FormattedContext;

                // Store product IDs in context for tracking
                ctx.SetData("rag_product_ids", ragResult.ProductIds);
                ctx.SetData("rag_metrics", ragResult.Metrics);

                _logger.LogInformation(
                    "RAG enabled: retrieved {Count} products in {Ms}ms",
                    ragResult.ProductIds.Count,
                    ragResult.Metrics.TotalLatency.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG retrieval failed, falling back to full-context");
                // Continue without RAG context (fallback)
            }
        }

        // Build prompt with CTA instruction
        var ctaInstruction = BuildCTAInstruction(ctx);
        var fullPrompt = $"{ctaInstruction}\n\nKhách hỏi: {message}";

        // Send to Gemini with RAG context
        var history = GetHistory(ctx);
        var response = await GeminiService.SendMessageAsync(
            ctx.FacebookPSID,
            fullPrompt,
            history,
            ragContext, // Pass RAG context
            cancellationToken: default);

        // Update history
        AddToHistory(ctx, "user", message);
        AddToHistory(ctx, "assistant", response);

        return response;
    }

    private string ExtractProductQuery(string message)
    {
        // Extract product-related query from message
        // Simple heuristic: if message contains product keywords, use it
        var productKeywords = new[] { "sản phẩm", "kem", "sữa", "serum", "toner" };

        if (productKeywords.Any(k => message.ToLower().Contains(k)))
        {
            return message;
        }

        return string.Empty;
    }
}
```

### Step 4: Feature Flag & Configuration (1 day)

**4.1 Create RAGOptions**

```csharp
// Configuration/RAGOptions.cs
namespace MessengerWebhook.Configuration;

public class RAGOptions
{
    public bool Enabled { get; set; } = false;
    public int TopK { get; set; } = 5;
    public string FallbackStrategy { get; set; } = "full-context";
    public int TimeoutMs { get; set; } = 5000;
}
```

**4.2 Update appsettings.json**

```json
{
  "RAG": {
    "Enabled": false,
    "TopK": 5,
    "FallbackStrategy": "full-context",
    "TimeoutMs": 5000
  }
}
```

**4.3 Update Program.cs**

```csharp
// Register RAG services
builder.Services.Configure<RAGOptions>(
    builder.Configuration.GetSection("RAG"));

builder.Services.AddScoped<ContextAssembler>();
builder.Services.AddScoped<IRAGService, RAGService>();
```

### Step 5: Testing (1-2 days)

**5.1 Integration Tests - RAG Pipeline**

```csharp
// tests/MessengerWebhook.IntegrationTests/Services/RAGIntegrationTests.cs
public class RAGIntegrationTests : IAsyncLifetime
{
    [Theory]
    [InlineData("kem chống nắng cho da dầu")]
    [InlineData("sữa rửa mặt cho da nhờn")]
    [InlineData("serum vitamin C")]
    public async Task RAGPipeline_VietnameseQueries_ReturnsRelevantContext(
        string query)
    {
        // Arrange
        await IndexTestProducts();

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5);

        // Assert
        Assert.NotNull(result.FormattedContext);
        Assert.NotEmpty(result.ProductIds);
        Assert.True(result.Metrics.TotalLatency.TotalMilliseconds < 1000,
            $"Latency {result.Metrics.TotalLatency.TotalMilliseconds}ms exceeds 1s");
    }

    [Fact]
    public async Task RAGPipeline_EndToEnd_WithGemini()
    {
        // Arrange
        await IndexTestProducts();
        var query = "Tôi cần kem chống nắng cho da dầu";

        // Act - RAG retrieval
        var ragResult = await _ragService.RetrieveContextAsync(query);

        // Act - Gemini with RAG context
        var response = await _geminiService.SendMessageAsync(
            "test-user",
            query,
            new List<ConversationMessage>(),
            ragResult.FormattedContext);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("Múi Xù", response); // Should mention retrieved product
    }
}
```

**5.2 E2E Tests - Conversation Flow**

```csharp
// tests/MessengerWebhook.IntegrationTests/RAGConversationFlowTests.cs
public class RAGConversationFlowTests : IAsyncLifetime
{
    [Fact]
    public async Task ConversationFlow_WithRAG_FindsProducts()
    {
        // Arrange
        EnableRAG();
        await IndexTestProducts();
        var psid = "test-user-rag";

        // Act - User asks about product
        var response1 = await SendMessage(psid, "Chào em");
        var response2 = await SendMessage(psid, "Em có kem chống nắng không?");

        // Assert
        Assert.Contains("Múi Xù", response2); // RAG should retrieve product
        Assert.DoesNotContain("Không tìm thấy", response2);
    }

    [Fact]
    public async Task ConversationFlow_RAGDisabled_UsesFullContext()
    {
        // Arrange
        DisableRAG();
        var psid = "test-user-no-rag";

        // Act
        var response = await SendMessage(psid, "Em có kem chống nắng không?");

        // Assert
        Assert.NotNull(response);
        // Should still work (fallback to full-context)
    }
}
```

**5.3 A/B Test Infrastructure**

```csharp
// Services/RAG/RAGABTestService.cs
public class RAGABTestService
{
    private readonly Random _random = new Random();
    private readonly double _ragEnabledPercentage;

    public RAGABTestService(IConfiguration configuration)
    {
        _ragEnabledPercentage = configuration.GetValue<double>(
            "RAG:ABTest:EnabledPercentage",
            0.0);
    }

    public bool ShouldUseRAG(string userId)
    {
        // Consistent hashing for same user
        var hash = userId.GetHashCode();
        var bucket = Math.Abs(hash % 100);

        return bucket < _ragEnabledPercentage;
    }
}
```

## Success Criteria

### Functional
- [ ] RAG retrieves top-5 products for Vietnamese queries
- [ ] Context assembled correctly (name, price, description)
- [ ] Gemini receives RAG context + user query
- [ ] Fallback to full-context on RAG failure
- [ ] Feature flag enables gradual rollout

### Performance
- [ ] End-to-end latency: <1s (p95)
- [ ] RAG retrieval: <200ms (p95)
- [ ] Token reduction: >90% vs full-context
- [ ] Cost: <$60/month (91.9% reduction)

### Quality
- [ ] All integration tests pass
- [ ] All E2E tests pass
- [ ] Vietnamese benchmark: 100% accuracy (13/13 queries)
- [ ] No regression in conversation quality

### Operational
- [ ] Feature flag works (enable/disable RAG)
- [ ] Metrics tracked (latency, accuracy, cost)
- [ ] Logging includes RAG pipeline details
- [ ] Graceful degradation on failures

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| RAG retrieval timeout | Medium | Medium | Set 5s timeout, fallback to full-context |
| Context too long (token limit) | Low | Medium | Limit to top-5 products, truncate descriptions |
| Gemini rejects RAG context | Low | High | Validate context format, test with Gemini API |
| Feature flag stuck on | Low | Critical | Add kill switch, monitor rollout percentage |
| Cost overrun | Low | Medium | Set budget alerts, monitor daily spend |

## Rollback Strategy

**Instant Rollback**:
```json
// appsettings.json
{
  "RAG": {
    "Enabled": false  // Instant rollback to full-context mode
  }
}
```

**Gradual Rollback**:
```json
{
  "RAG": {
    "ABTest": {
      "EnabledPercentage": 10  // Reduce from 50% to 10%
    }
  }
}
```

**No Code Changes Needed**: Feature flag allows instant rollback without deployment.

## Gradual Rollout Plan

**Week 1**: 10% of users
- Monitor metrics: latency, accuracy, error rate
- Validate cost reduction
- Fix critical issues

**Week 2**: 50% of users
- A/B test: RAG vs full-context
- Compare conversation quality
- Tune parameters (topK, timeout)

**Week 3**: 100% of users
- Full rollout
- Monitor for 1 week
- Document lessons learned

## Next Steps

After Phase 5 completion:
1. **Phase 6**: Optimize and monitor production metrics
2. **Documentation**: Update system architecture docs
3. **Training**: Train team on RAG system

## Unresolved Questions

1. **Query Extraction**: How to extract product query from conversational message? (e.g., "Em có kem chống nắng không?" → "kem chống nắng")
2. **Context Length**: Should we truncate product descriptions to fit token budget?
3. **Multi-Product Queries**: How to handle "iPhone vs Samsung" comparisons?
4. **Conversation History**: Should RAG consider previous messages for retrieval?
5. **Freeship Policy**: Should we add freeship info to RAG context? (from root-cause analysis)
