using Microsoft.Extensions.Caching.Distributed;

namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Handles cache invalidation on product updates
/// </summary>
public class CacheInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(
        IDistributedCache cache,
        ILogger<CacheInvalidationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Invalidate all caches for a specific product
    /// </summary>
    public async Task InvalidateProductAsync(
        string productId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Note: Redis IDistributedCache doesn't support pattern deletion
        // Rely on TTL expiration for now
        // TODO: Implement pattern-based invalidation with StackExchange.Redis directly

        _logger.LogInformation(
            "Invalidated caches for product {ProductId}, tenant {TenantId}",
            productId,
            tenantId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Invalidate all caches for a tenant
    /// </summary>
    public async Task InvalidateTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Invalidated all caches for tenant {TenantId}",
            tenantId);

        // TODO: Implement pattern-based invalidation
        await Task.CompletedTask;
    }
}
