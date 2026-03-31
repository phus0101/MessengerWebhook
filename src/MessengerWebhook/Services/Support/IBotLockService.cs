using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Support;

public interface IBotLockService
{
    Task<bool> IsLockedAsync(string facebookPsid, CancellationToken cancellationToken = default);
    Task LockAsync(string facebookPsid, string reason, Guid? supportCaseId = null, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string facebookPsid, CancellationToken cancellationToken = default);
    Task ExtendLockAsync(string facebookPsid, int additionalMinutes, CancellationToken cancellationToken = default);
    Task<List<BotConversationLock>> GetActiveLocksAsync(CancellationToken cancellationToken = default);
    Task<List<BotConversationLock>> GetLockHistoryAsync(string facebookPsid, CancellationToken cancellationToken = default);
}
