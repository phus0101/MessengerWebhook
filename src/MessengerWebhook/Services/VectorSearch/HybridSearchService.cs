using MessengerWebhook.Services.AI.Embeddings;

namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Hybrid search combining vector similarity and keyword search via RRF fusion
/// </summary>
public class HybridSearchService : IHybridSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly KeywordSearchService _keywordSearch;
    private readonly RRFFusionService _rrfFusion;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        KeywordSearchService keywordSearch,
        RRFFusionService rrfFusion,
        ILogger<HybridSearchService> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _keywordSearch = keywordSearch;
        _rrfFusion = rrfFusion;
        _logger = logger;
    }

    public async Task<List<FusedResult>> SearchAsync(
        string query,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Execute searches in parallel
        var vectorTask = SearchVectorAsync(query, topK * 2, filter, cancellationToken);
        var keywordTask = _keywordSearch.SearchAsync(query, topK * 2, cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask);

        var vectorResults = await vectorTask;
        var keywordResults = await keywordTask;

        // Merge with RRF
        var fusedResults = _rrfFusion.Fuse(
            new List<List<ProductSearchResult>> { vectorResults, keywordResults },
            topK);

        stopwatch.Stop();

        _logger.LogInformation(
            "Hybrid search: {Query} → {VectorCount} vector + {KeywordCount} keyword → {FusedCount} fused in {Ms}ms",
            query,
            vectorResults.Count,
            keywordResults.Count,
            fusedResults.Count,
            stopwatch.ElapsedMilliseconds);

        return fusedResults;
    }

    private async Task<List<ProductSearchResult>> SearchVectorAsync(
        string query,
        int topK,
        Dictionary<string, object>? filter,
        CancellationToken cancellationToken)
    {
        // Generate query embedding
        var embedding = await _embeddingService.EmbedAsync(
            query,
            cancellationToken);

        // Search Pinecone
        var results = await _vectorSearch.SearchSimilarAsync(
            embedding,
            topK,
            filter,
            cancellationToken);

        return results;
    }
}
