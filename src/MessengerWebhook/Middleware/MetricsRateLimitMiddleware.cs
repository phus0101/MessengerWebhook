using System.Collections.Concurrent;

namespace MessengerWebhook.Middleware;

public class MetricsRateLimitMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsRateLimitMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimits = new();
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private static readonly int _permitLimit = 10;
    private readonly Timer _cleanupTimer;

    public MetricsRateLimitMiddleware(RequestDelegate next, ILogger<MetricsRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        // H3 Fix: Cleanup expired entries every hour to prevent memory leak
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to metrics endpoints
        if (!context.Request.Path.StartsWithSegments("/admin/api/metrics"))
        {
            await _next(context);
            return;
        }

        var tenantId = context.User.Identity?.Name ?? "anonymous";
        var key = $"metrics:{tenantId}";

        var now = DateTimeOffset.UtcNow;
        var rateLimitInfo = _rateLimits.GetOrAdd(key, _ => new RateLimitInfo());

        bool isRateLimited = false;
        int retryAfterSeconds = 0;

        // H2 Fix: Use SemaphoreSlim to prevent race conditions
        await rateLimitInfo.Semaphore.WaitAsync();
        try
        {
            // Reset window if expired
            if (now - rateLimitInfo.WindowStart >= _window)
            {
                rateLimitInfo.WindowStart = now;
                rateLimitInfo.RequestCount = 0;
            }

            // Check limit
            if (rateLimitInfo.RequestCount >= _permitLimit)
            {
                isRateLimited = true;
                retryAfterSeconds = (int)(_window - (now - rateLimitInfo.WindowStart)).TotalSeconds;
            }
            else
            {
                rateLimitInfo.RequestCount++;
            }

            rateLimitInfo.LastAccessed = now;
        }
        finally
        {
            rateLimitInfo.Semaphore.Release();
        }

        if (isRateLimited)
        {
            _logger.LogWarning("Rate limit exceeded for {TenantId} on metrics endpoint", tenantId);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = "Too many requests. Please try again later.",
                retryAfter = retryAfterSeconds
            });
            return;
        }

        await _next(context);
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _rateLimits)
        {
            // Remove entries not accessed in the last 2 hours
            if (now - kvp.Value.LastAccessed > TimeSpan.FromHours(2))
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_rateLimits.TryRemove(key, out var info))
            {
                info.Dispose();
                _logger.LogDebug("Cleaned up expired rate limit entry for key: {Key}", key);
            }
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();

        // Dispose all semaphores
        foreach (var kvp in _rateLimits)
        {
            kvp.Value.Dispose();
        }
        _rateLimits.Clear();
    }

    private class RateLimitInfo : IDisposable
    {
        public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
        public DateTimeOffset WindowStart { get; set; } = DateTimeOffset.UtcNow;
        public int RequestCount { get; set; }
        public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

        public void Dispose()
        {
            Semaphore?.Dispose();
        }
    }
}
