namespace MessengerWebhook.Services.Support;

public interface IBotLockService
{
    Task<bool> IsLockedAsync(string facebookPsid, CancellationToken cancellationToken = default);
    Task LockAsync(string facebookPsid, string reason, Guid? supportCaseId = null, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string facebookPsid, CancellationToken cancellationToken = default);
}
