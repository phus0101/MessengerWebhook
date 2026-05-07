using MessengerWebhook.Configuration;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MessengerWebhook.Services.RAG;

/// <summary>
/// Orchestrates RAG pipeline: hybrid search → context assembly → metrics
/// </summary>
public class RAGService : IRAGService
{
    private readonly IHybridSearchService _hybridSearch;
    private readonly IContextAssembler _contextAssembler;
    private readonly ITenantContext _tenantContext;
    private readonly RAGOptions _options;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IHybridSearchService hybridSearch,
        IContextAssembler contextAssembler,
        IOptions<RAGOptions> options,
        ILogger<RAGService> logger)
        : this(hybridSearch, contextAssembler, new NullTenantContext(), options, logger)
    {
    }

    public RAGService(
        IHybridSearchService hybridSearch,
        IContextAssembler contextAssembler,
        ITenantContext tenantContext,
        IOptions<RAGOptions> options,
        ILogger<RAGService> logger)
    {
        _hybridSearch = hybridSearch;
        _contextAssembler = contextAssembler;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RAGContext> RetrieveContextAsync(
        string query,
        int topK = 5,
        bool includeDetailedInfo = false,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var retrievalStopwatch = Stopwatch.StartNew();

        try
        {
            if (!_tenantContext.TenantId.HasValue)
            {
                retrievalStopwatch.Stop();
                _logger.LogWarning("RAG retrieval skipped because tenant context is not resolved");
                return CreateEmptyContext(totalStopwatch.Elapsed, retrievalStopwatch.Elapsed);
            }

            var filter = new Dictionary<string, object>
            {
                ["tenant_id"] = _tenantContext.TenantId.Value.ToString()
            };

            // Step 1: Hybrid search
            var results = await _hybridSearch.SearchAsync(
                query,
                topK,
                filter,
                cancellationToken);

            retrievalStopwatch.Stop();

            if (results.Count == 0)
            {
                _logger.LogWarning("No products found for query: {Query}", query);
                return CreateEmptyContext(totalStopwatch.Elapsed, retrievalStopwatch.Elapsed);
            }

            // Step 2: Context assembly
            var productIds = results.Select(r => r.ProductId).ToList();
            var assembledContext = await _contextAssembler.AssembleContextAsync(
                productIds,
                includeDetailedInfo,
                cancellationToken);

            totalStopwatch.Stop();

            // Step 3: Metrics
            var metrics = new RAGMetrics(
                RetrievalLatency: retrievalStopwatch.Elapsed,
                TotalLatency: totalStopwatch.Elapsed,
                ProductsRetrieved: assembledContext.ProductIds.Count,
                CacheHit: false, // Will be set by cache layer
                Source: assembledContext.ProductIds.Count > 0 ? "hybrid" : "empty");

            _logger.LogInformation(
                "RAG retrieval completed: {Count} products, {Latency}ms",
                assembledContext.ProductIds.Count,
                totalStopwatch.ElapsedMilliseconds);

            return new RAGContext(assembledContext.FormattedContext, assembledContext.ProductIds, assembledContext.Products, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed for query: {Query}", query);

            // Fallback strategy
            return await HandleFallbackAsync(query, totalStopwatch.Elapsed, cancellationToken);
        }
    }

    private async Task<RAGContext> HandleFallbackAsync(
        string query,
        TimeSpan totalLatency,
        CancellationToken cancellationToken)
    {
        if (_options.FallbackStrategy == "full-context")
        {
            _logger.LogWarning("Falling back to full-context mode");
            // Return empty context - caller will use full product catalog
            return CreateEmptyContext(totalLatency, TimeSpan.Zero, source: "fallback");
        }

        // Default: return empty context
        return CreateEmptyContext(totalLatency, TimeSpan.Zero, source: "error");
    }

    private RAGContext CreateEmptyContext(
        TimeSpan totalLatency,
        TimeSpan retrievalLatency,
        string source = "empty")
    {
        var metrics = new RAGMetrics(
            RetrievalLatency: retrievalLatency,
            TotalLatency: totalLatency,
            ProductsRetrieved: 0,
            CacheHit: false,
            Source: source);

        return new RAGContext(
            "Không tìm thấy sản phẩm phù hợp.",
            new List<string>(),
            new List<MessengerWebhook.Services.ProductGrounding.GroundedProduct>(),
            metrics);
    }
}
