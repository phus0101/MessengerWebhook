using MessengerWebhook.Configuration;
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
    private readonly RAGOptions _options;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IHybridSearchService hybridSearch,
        IContextAssembler contextAssembler,
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
        var totalStopwatch = Stopwatch.StartNew();
        var retrievalStopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Hybrid search
            var results = await _hybridSearch.SearchAsync(
                query,
                topK,
                filter: null,
                cancellationToken);

            retrievalStopwatch.Stop();

            if (results.Count == 0)
            {
                _logger.LogWarning("No products found for query: {Query}", query);
                return CreateEmptyContext(totalStopwatch.Elapsed, retrievalStopwatch.Elapsed);
            }

            // Step 2: Context assembly
            var productIds = results.Select(r => r.ProductId).ToList();
            var formattedContext = await _contextAssembler.AssembleContextAsync(
                productIds,
                cancellationToken);

            totalStopwatch.Stop();

            // Step 3: Metrics
            var metrics = new RAGMetrics(
                RetrievalLatency: retrievalStopwatch.Elapsed,
                TotalLatency: totalStopwatch.Elapsed,
                ProductsRetrieved: results.Count,
                CacheHit: false, // Will be set by cache layer
                Source: "hybrid");

            _logger.LogInformation(
                "RAG retrieval completed: {Count} products, {Latency}ms",
                results.Count,
                totalStopwatch.ElapsedMilliseconds);

            return new RAGContext(formattedContext, productIds, metrics);
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
            metrics);
    }
}
