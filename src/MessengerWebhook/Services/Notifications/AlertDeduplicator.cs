using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services.Notifications;

/// <summary>
/// Singleton in-memory dedup: same alert type won't fire more than once per window.
/// Prevents Telegram spam during sustained incidents.
/// </summary>
public sealed class AlertDeduplicator
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache _cache;

    public AlertDeduplicator(IMemoryCache cache) => _cache = cache;

    /// <summary>
    /// Returns true if the alert should be sent (first occurrence in window).
    /// Returns false if it's a duplicate within the dedup window.
    /// </summary>
    public bool TryAcquire(string alertType)
    {
        var key = $"alert_dedup:{alertType}";
        if (_cache.TryGetValue(key, out _))
            return false;

        _cache.Set(key, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Window,
            Size = 1
        });
        return true;
    }
}
