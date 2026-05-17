namespace MessengerWebhook.Services.Cache;

/// <summary>
/// Redis-backed semantic answer cache for policy/FAQ sub-intent responses.
/// Avoids redundant LLM calls when the same sub-intent type is asked repeatedly
/// within the same tenant context.
/// </summary>
public interface ISemanticAnswerCache
{
    /// <summary>
    /// Try to retrieve a cached answer for the given sub-intent + tenant combination.
    /// Returns null on cache miss.
    /// </summary>
    Task<string?> GetAsync(string subIntentKey, string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Store an LLM answer in the cache under the given sub-intent + tenant key.
    /// </summary>
    Task SetAsync(string subIntentKey, string tenantId, string answer, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Invalidate cached answer for the given sub-intent + tenant combination.
    /// </summary>
    Task InvalidateAsync(string subIntentKey, string tenantId, CancellationToken ct = default);
}
