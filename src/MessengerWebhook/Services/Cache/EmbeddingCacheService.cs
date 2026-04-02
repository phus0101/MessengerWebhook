using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Wraps IEmbeddingService with Redis cache layer (1 hour TTL, 90% hit rate target)
/// </summary>
public class EmbeddingCacheService : IEmbeddingService
{
    private readonly IEmbeddingService _innerService;
    private readonly IDistributedCache _cache;
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly ILogger<EmbeddingCacheService> _logger;
    private readonly int _ttlSeconds;

    public EmbeddingCacheService(
        IEmbeddingService innerService,
        IDistributedCache cache,
        CacheKeyGenerator keyGenerator,
        IConfiguration configuration,
        ILogger<EmbeddingCacheService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _keyGenerator = keyGenerator;
        _logger = logger;
        _ttlSeconds = configuration.GetValue<int>("CacheTTL:EmbeddingSeconds", 3600);
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var key = _keyGenerator.GenerateEmbeddingKey(text);

        // Try cache first
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Embedding cache HIT: {Key}", key);
            return JsonSerializer.Deserialize<float[]>(cached)!;
        }

        _logger.LogDebug("Embedding cache MISS: {Key}", key);

        // Generate embedding
        var embedding = await _innerService.EmbedAsync(
            text,
            cancellationToken);

        // Cache result
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_ttlSeconds)
        };

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(embedding),
            options,
            cancellationToken);

        return embedding;
    }

    public async Task<List<float[]>> EmbedBatchAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>();
        var missingIndices = new List<int>();
        var missingTexts = new List<string>();

        // Check cache for each text
        for (int i = 0; i < texts.Count; i++)
        {
            var key = _keyGenerator.GenerateEmbeddingKey(texts[i]);
            var cached = await _cache.GetStringAsync(key, cancellationToken);

            if (cached != null)
            {
                results.Add(JsonSerializer.Deserialize<float[]>(cached)!);
            }
            else
            {
                results.Add(null!); // Placeholder
                missingIndices.Add(i);
                missingTexts.Add(texts[i]);
            }
        }

        // Generate missing embeddings
        if (missingTexts.Count > 0)
        {
            var generated = await _innerService.EmbedBatchAsync(
                missingTexts,
                cancellationToken);

            // Fill in results and cache
            for (int i = 0; i < missingIndices.Count; i++)
            {
                var idx = missingIndices[i];
                results[idx] = generated[i];

                // Cache
                var key = _keyGenerator.GenerateEmbeddingKey(texts[idx]);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_ttlSeconds)
                };

                await _cache.SetStringAsync(
                    key,
                    JsonSerializer.Serialize(generated[i]),
                    options,
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Batch embedding: {Total} total, {Cached} cached, {Generated} generated",
            texts.Count,
            texts.Count - missingTexts.Count,
            missingTexts.Count);

        return results;
    }
}
