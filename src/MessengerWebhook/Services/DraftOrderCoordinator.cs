using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MessengerWebhook.Services;

/// <summary>
/// Coordinates draft order creation with race-condition protection and idempotency.
/// Prevents duplicate draft orders when multiple concurrent paths attempt creation.
/// </summary>
public class DraftOrderCoordinator
{
    private readonly IDraftOrderService _draftOrderService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DraftOrderCoordinator> _logger;

    private static readonly SemaphoreSlim _globalLock = new(1, 1);

    public DraftOrderCoordinator(
        IDraftOrderService draftOrderService,
        IMemoryCache cache,
        ILogger<DraftOrderCoordinator> logger)
    {
        _draftOrderService = draftOrderService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Creates a draft order from the current context and sets the resulting IDs and state on ctx.
    /// Uses a global lock to prevent race-condition duplicates, then caches the result by session
    /// so subsequent calls return the existing draft instead of creating a new one.
    /// </summary>
    /// <returns>The created DraftOrder, or null if creation fails.</returns>
    public async Task<DraftOrder?> FinalizeDraftOrderAsync(StateContext ctx, CancellationToken ct = default)
    {
        var sessionId = ctx.SessionId;

        // Check idempotency cache first
        if (_cache.TryGetValue(sessionId, out DraftOrder? cached))
        {
            _logger.LogDebug("Returning cached draft order {DraftCode} for session {SessionId}",
                cached?.DraftCode, sessionId);
            return cached;
        }

        try
        {
            await _globalLock.WaitAsync(ct);

            // Check again under lock (double-checked locking)
            if (_cache.TryGetValue(sessionId, out DraftOrder? cachedUnderLock))
            {
                _logger.LogDebug("Returning cached draft order {DraftCode} for session {SessionId} (under lock)",
                    cachedUnderLock.DraftCode, sessionId);
                return cachedUnderLock;
            }

            _logger.LogInformation("Creating draft order for session {SessionId} (PSID: {PSID})",
                sessionId, ctx.FacebookPSID);

            var draft = await _draftOrderService.CreateFromContextAsync(ctx, ct);

            // Set results on context
            ctx.SetData("draftOrderId", draft.Id);
            ctx.SetData("draftOrderCode", draft.DraftCode);
            ctx.CurrentState = ConversationState.Complete;

            // Cache the result for idempotency (till end of request/session)
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                Size = 1
            };
            _cache.Set(sessionId, draft, cacheEntryOptions);

            _logger.LogInformation("Draft order {DraftCode} created and cached for session {SessionId}",
                draft.DraftCode, sessionId);

            return draft;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create draft order for session {SessionId}", sessionId);
            return null;
        }
        finally
        {
            _globalLock.Release();
        }
    }
}
