using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services;

public class SessionManager : ISessionManager
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionManager> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public SessionManager(
        ISessionRepository sessionRepository,
        IMemoryCache cache,
        ILogger<SessionManager> logger)
    {
        _sessionRepository = sessionRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ConversationSession?> GetAsync(string psid)
    {
        var cacheKey = GetCacheKey(psid);

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out ConversationSession? cachedSession))
        {
            _logger.LogDebug("Session cache hit for PSID: {PSID}", psid);
            return cachedSession;
        }

        // Fallback to database
        _logger.LogDebug("Session cache miss for PSID: {PSID}, fetching from database", psid);
        var session = await _sessionRepository.GetByPSIDAsync(psid);

        if (session != null)
        {
            // Update cache
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(CacheDuration)
                .SetSize(1);

            _cache.Set(cacheKey, session, cacheOptions);
        }

        return session;
    }

    public async Task SaveAsync(ConversationSession session)
    {
        // Save to database
        await _sessionRepository.UpdateAsync(session);

        // Update cache
        var cacheKey = GetCacheKey(session.FacebookPSID);
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(CacheDuration)
            .SetSize(1);

        _cache.Set(cacheKey, session, cacheOptions);

        _logger.LogDebug("Session saved and cached for PSID: {PSID}", session.FacebookPSID);
    }

    public async Task DeleteAsync(string psid)
    {
        // Remove from cache
        var cacheKey = GetCacheKey(psid);
        _cache.Remove(cacheKey);

        // Note: Database deletion is handled by SessionCleanupService
        // This method only evicts from cache
        _logger.LogDebug("Session cache evicted for PSID: {PSID}", psid);

        await Task.CompletedTask;
    }

    private static string GetCacheKey(string psid) => $"session:{psid}";
}
