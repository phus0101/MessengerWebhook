using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.RAG.Reranking;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Hybrid search combining vector similarity and keyword search via RRF fusion,
/// with optional Cohere reranking for improved relevance.
/// </summary>
public class HybridSearchService : IHybridSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearch;
    private readonly KeywordSearchService _keywordSearch;
    private readonly RRFFusionService _rrfFusion;
    private readonly IRerankService _rerankService;
    private readonly CohereOptions _cohereOptions;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearch,
        KeywordSearchService keywordSearch,
        RRFFusionService rrfFusion,
        ILogger<HybridSearchService> logger,
        IRerankService rerankService,
        IOptions<CohereOptions> cohereOptions)
    {
        _embeddingService = embeddingService;
        _vectorSearch = vectorSearch;
        _keywordSearch = keywordSearch;
        _rrfFusion = rrfFusion;
        _logger = logger;
        _rerankService = rerankService;
        _cohereOptions = cohereOptions.Value;
    }

    public async Task<List<FusedResult>> SearchAsync(
        string query,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // When reranking is enabled, fetch more candidates for the reranker to select from
        var candidateK = _cohereOptions.Enabled
            ? topK * _cohereOptions.CandidateMultiplier
            : topK * 2;

        // Execute searches in parallel
        var vectorTask = SearchVectorAsync(query, candidateK, filter, cancellationToken);
        var keywordTask = _keywordSearch.SearchAsync(query, candidateK, ResolveTenantId(filter), cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask);

        var vectorResults = await vectorTask;
        var keywordResults = await keywordTask;

        // Merge with RRF — fuse up to candidateK, then rerank selects topK
        var fusedResults = _rrfFusion.Fuse(
            new List<List<ProductSearchResult>> { vectorResults, keywordResults },
            _cohereOptions.Enabled ? candidateK : topK);

        var rerankApplied = false;

        // Rerank fused candidates when enabled and there are more results than needed
        if (_cohereOptions.Enabled && fusedResults.Count > topK)
        {
            var candidates = fusedResults
                .Select(r => new RankableDocument(r.ProductId, $"{r.Name} {r.Category}"))
                .ToList();

            var ranked = await _rerankService.RerankAsync(query, candidates, topK, cancellationToken);

            // Rebuild result list in reranked order, preserving FusedResult metadata
            var fusedById = fusedResults.ToDictionary(r => r.ProductId);
            fusedResults = ranked
                .Where(r => fusedById.ContainsKey(r.Id))
                .Select(r => fusedById[r.Id])
                .ToList();

            rerankApplied = true;
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "HybridSearch Query={Query} VectorCount={VectorCount} KeywordCount={KeywordCount} FusedCount={FusedCount} RerankApplied={RerankApplied} ElapsedMs={ElapsedMs}",
            query,
            vectorResults.Count,
            keywordResults.Count,
            fusedResults.Count,
            rerankApplied,
            stopwatch.ElapsedMilliseconds);

        return fusedResults;
    }

    private static Guid? ResolveTenantId(Dictionary<string, object>? filter)
    {
        if (filter?.TryGetValue("tenant_id", out var value) != true)
        {
            return null;
        }

        return value switch
        {
            Guid tenantId => tenantId,
            string tenantIdText when Guid.TryParse(tenantIdText, out var tenantId) => tenantId,
            _ => null
        };
    }

    private async Task<List<ProductSearchResult>> SearchVectorAsync(
        string query,
        int topK,
        Dictionary<string, object>? filter,
        CancellationToken cancellationToken)
    {
        var embedding = await _embeddingService.EmbedAsync(query, cancellationToken);
        return await _vectorSearch.SearchSimilarAsync(embedding, topK, filter, cancellationToken);
    }
}
