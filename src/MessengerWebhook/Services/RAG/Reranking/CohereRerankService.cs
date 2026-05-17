using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.RAG.Reranking;

/// <summary>
/// Calls the Cohere v2 Rerank REST API directly via IHttpClientFactory.
/// The Cohere .NET SDK requires .NET 10, so we use raw HTTP instead.
/// </summary>
public class CohereRerankService : IRerankService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly CohereOptions _options;
    private readonly ILogger<CohereRerankService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public CohereRerankService(
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache,
        ITenantContext tenantContext,
        IOptions<CohereOptions> options,
        ILogger<CohereRerankService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RankedDocument>> RerankAsync(
        string query,
        IReadOnlyList<RankableDocument> candidates,
        int topN,
        CancellationToken ct = default)
    {
        // H1: guard against invalid topN
        topN = Math.Max(1, topN);

        // Skip API when candidates already fit within topN
        if (candidates.Count <= topN)
        {
            return candidates
                .Select(c => new RankedDocument(c.Id, c.Text, 1.0))
                .ToList();
        }

        var cacheKey = BuildCacheKey(query, candidates, topN);

        // Check cache
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                var cachedResult = JsonSerializer.Deserialize<List<RankedDocument>>(cached, _jsonOptions);
                if (cachedResult is not null)
                {
                    _logger.LogInformation(
                        "RerankCompleted Provider=Cohere Model={Model} ElapsedMs=0 CandidateCount={CandidateCount} TopN={TopN} CacheHit=true",
                        _options.RerankModel, candidates.Count, topN);
                    return cachedResult;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rerank cache read failed; proceeding to API call");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var documents = candidates.Select(c => c.Text).ToList();
            var requestBody = new
            {
                model = _options.RerankModel,
                query,
                documents,
                top_n = topN,
                return_documents = false
            };

            var client = _httpClientFactory.CreateClient("cohere");
            using var response = await client.PostAsync(
                "v2/rerank",
                new StringContent(
                    JsonSerializer.Serialize(requestBody, _jsonOptions),
                    Encoding.UTF8,
                    "application/json"),
                ct);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var rerankResponse = JsonSerializer.Deserialize<RerankResponse>(responseBody, _jsonOptions)
                ?? throw new InvalidOperationException("Cohere returned null rerank response");

            // M2: bounds-check index to guard against malformed API response
            var ranked = rerankResponse.Results
                .Where(r => r.Index >= 0 && r.Index < candidates.Count)
                .Select(r => new RankedDocument(candidates[r.Index].Id, candidates[r.Index].Text, r.RelevanceScore))
                .ToList();

            sw.Stop();
            _logger.LogInformation(
                "RerankCompleted Provider=Cohere Model={Model} ElapsedMs={ElapsedMs} CandidateCount={CandidateCount} TopN={TopN} CacheHit=false",
                _options.RerankModel, sw.ElapsedMilliseconds, candidates.Count, topN);

            // Cache the result
            try
            {
                var serialized = JsonSerializer.Serialize(ranked, _jsonOptions);
                await _cache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheTtlMinutes)
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rerank cache write failed; result still returned");
            }

            return ranked;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex,
                "Cohere rerank failed after {ElapsedMs}ms; falling back to original order (topN={TopN})",
                sw.ElapsedMilliseconds, topN);

            // Fallback: return first topN candidates in original order with score=0
            return candidates
                .Take(topN)
                .Select(c => new RankedDocument(c.Id, c.Text, 0.0))
                .ToList();
        }
    }

    private string BuildCacheKey(string query, IReadOnlyList<RankableDocument> candidates, int topN)
    {
        var queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(query)));
        // C1: include candidate IDs hash to prevent stale cache hits when catalog changes
        var docsHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(candidates.Select(c => c.Id)))));
        var tenantId = _tenantContext.TenantId?.ToString() ?? "none";
        return $"rerank:{queryHash}:{tenantId}:{topN}:{docsHash}";
    }

    // Private DTOs for Cohere v2 rerank response deserialization
    private sealed record RerankResponse(
        [property: JsonPropertyName("results")] List<RerankResult> Results);

    private sealed record RerankResult(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("relevance_score")] double RelevanceScore);
}
