using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Redis-backed implementation of ISemanticAnswerCache.
/// Key format: "semantic:{subIntentKey}:{tenantId}"
/// Default TTL: 6 hours — policy/FAQ answers change infrequently.
/// Falls back gracefully when Redis is unavailable (logs warning, returns null).
/// </summary>
public sealed class SemanticAnswerCache : ISemanticAnswerCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<SemanticAnswerCache> _logger;

    // Key prefix to avoid collisions with ResultCacheService / EmbeddingCacheService
    private const string KeyPrefix = "semantic";

    public SemanticAnswerCache(IDistributedCache cache, ILogger<SemanticAnswerCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> GetAsync(string subIntentKey, string tenantId, CancellationToken ct = default)
    {
        var key = BuildKey(subIntentKey, tenantId);
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is null)
            {
                _logger.LogDebug("SemanticCache MISS: {Key}", key);
                return null;
            }

            var answer = Encoding.UTF8.GetString(bytes);
            _logger.LogInformation("SemanticCache Hit: subIntentKey={SubIntentKey} tenant={TenantId}", subIntentKey, tenantId);
            return answer;
        }
        catch (Exception ex)
        {
            // Never let cache failures surface to callers — degrade to LLM call
            _logger.LogWarning(ex, "SemanticCache GET failed for key {Key}; falling back to LLM", key);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string subIntentKey, string tenantId, string answer, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return;

        var key = BuildKey(subIntentKey, tenantId);
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            await _cache.SetAsync(key, Encoding.UTF8.GetBytes(answer), options, ct);
            _logger.LogDebug("SemanticCache SET: key={Key} ttl={Ttl}", key, ttl);
        }
        catch (Exception ex)
        {
            // Non-fatal — the caller already has the answer; just skip caching
            _logger.LogWarning(ex, "SemanticCache SET failed for key {Key}; answer will not be cached", key);
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(string subIntentKey, string tenantId, CancellationToken ct = default)
    {
        var key = BuildKey(subIntentKey, tenantId);
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogInformation("SemanticCache invalidated: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SemanticCache INVALIDATE failed for key {Key}", key);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildKey(string subIntentKey, string tenantId)
        => $"{KeyPrefix}:{subIntentKey}:{tenantId}";
}
