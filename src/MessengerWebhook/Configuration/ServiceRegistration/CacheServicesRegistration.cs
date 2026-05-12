using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Cache;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Caching.Distributed;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class CacheServicesRegistration
{
    /// <summary>
    /// Must be called AFTER AddAiServices so concrete types (VertexAIEmbeddingService,
    /// HybridSearchService) are already registered before the decorator overrides.
    /// </summary>
    internal static IServiceCollection AddCacheServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.PropertyNameCaseInsensitive = true);

        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100_000;
            options.CompactionPercentage = 0.25;
        });

        services.AddResponseCaching();

        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled", false);
        if (!redisEnabled)
            return services;

        var redisConnectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
            return services;

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = configuration["Redis:InstanceName"];
        });

        services.AddSingleton<CacheKeyGenerator>();
        services.AddScoped<CacheInvalidationService>();

        // Decorate IEmbeddingService with caching (concrete type must be registered first by AddAiServices)
        services.AddScoped<IEmbeddingService>(sp =>
        {
            var inner = sp.GetRequiredService<VertexAIEmbeddingService>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var keyGenerator = sp.GetRequiredService<CacheKeyGenerator>();
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<EmbeddingCacheService>>();
            return new EmbeddingCacheService(inner, cache, keyGenerator, config, logger);
        });

        // Decorate IHybridSearchService with caching (concrete type must be registered first by AddAiServices)
        services.AddScoped<IHybridSearchService>(sp =>
        {
            var inner = sp.GetRequiredService<HybridSearchService>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            var keyGenerator = sp.GetRequiredService<CacheKeyGenerator>();
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<ResultCacheService>>();
            return new ResultCacheService(inner, cache, keyGenerator, tenantContext, config, logger);
        });

        return services;
    }
}
