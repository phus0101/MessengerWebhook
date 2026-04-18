using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MessengerWebhook.Endpoints;

public static class MetricsEndpointExtensions
{
    public static RouteGroupBuilder MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/metrics")
            .RequireAuthorization();

        group.MapGet("/summary", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:summary:{user.TenantId}:{start:yyyyMMddHHmmss}:{end:yyyyMMddHHmmss}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<MetricsSummary>(cached);
                return Results.Ok(cachedResult);
            }

            var summary = await metricsService.GetSummaryAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(summary),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(summary);
        })
        .WithName("GetMetricsSummary");

        group.MapGet("/variants", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:variants:{user.TenantId}:{start:yyyyMMddHHmmss}:{end:yyyyMMddHHmmss}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<VariantComparison>(cached);
                return Results.Ok(cachedResult);
            }

            var comparison = await metricsService.GetVariantComparisonAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(comparison),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(comparison);
        })
        .WithName("GetVariantComparison");

        group.MapGet("/pipeline", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:pipeline:{user.TenantId}:{start:yyyyMMddHHmmss}:{end:yyyyMMddHHmmss}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<PipelinePerformance>(cached);
                return Results.Ok(cachedResult);
            }

            var performance = await metricsService.GetPipelinePerformanceAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(performance),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(performance);
        })
        .WithName("GetPipelinePerformance");

        group.MapGet("/trends", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:trends:{user.TenantId}:{start:yyyyMMddHHmmss}:{end:yyyyMMddHHmmss}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<List<ConversationTrendDto>>(cached);
                return Results.Ok(cachedResult);
            }

            var trends = await metricsService.GetConversationTrendsAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(trends),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(trends);
        })
        .WithName("GetConversationTrends");

        return group;
    }
}
