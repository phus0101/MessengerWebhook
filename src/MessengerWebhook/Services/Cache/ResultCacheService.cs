using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using MessengerWebhook.Services.Tenants;

namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Wraps IHybridSearchService with Redis cache layer (15 min TTL, 70% hit rate target)
/// </summary>
public class ResultCacheService : IHybridSearchService
{
    private readonly IHybridSearchService _innerService;
    private readonly IDistributedCache _cache;
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ResultCacheService> _logger;
    private readonly int _ttlSeconds;

    public ResultCacheService(
        IHybridSearchService innerService,
        IDistributedCache cache,
        CacheKeyGenerator keyGenerator,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<ResultCacheService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _tenantContext = tenantContext;
        _logger = logger;
        _ttlSeconds = configuration.GetValue<int>("CacheTTL:ResultSeconds", 900);
    }

    public async Task<List<FusedResult>> SearchAsync(
        string query,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        // Validate tenant context
        if (_tenantContext.TenantId == Guid.Empty)
        {
            _logger.LogWarning("TenantId is empty, bypassing cache");
            return await _innerService.SearchAsync(query, topK, filter, cancellationToken);
        }

        // Generate cache key (simplified - use query hash instead of embedding)
        var filterStr = filter != null ? System.Text.Json.JsonSerializer.Serialize(filter) : "none";
        var key = _keyGenerator.GenerateResponseKey(
            query,
            _tenantContext.TenantId.ToString()!,
            new List<string> { filterStr });

        // Try cache first
        var cached = await _cache.GetAsync(key, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Result cache HIT: {Key}", key);
            var cachedJson = System.Text.Encoding.UTF8.GetString(cached);
            return JsonSerializer.Deserialize<List<FusedResult>>(cachedJson)!;
        }

        _logger.LogDebug("Result cache MISS: {Key}", key);

        // Execute search
        var results = await _innerService.SearchAsync(
            query,
            topK,
            filter,
            cancellationToken);

        // Cache results
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_ttlSeconds)
        };

        var resultJson = JsonSerializer.Serialize(results);
        await _cache.SetAsync(
            key,
            System.Text.Encoding.UTF8.GetBytes(resultJson),
            options,
            cancellationToken);

        return results;
    }
}
