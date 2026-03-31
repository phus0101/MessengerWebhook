namespace MessengerWebhook.Services.Admin;

public interface IAdminAuditService
{
    Task LogAsync(
        AdminUserContext actor,
        string action,
        string resourceType,
        string resourceId,
        string? details = null,
        CancellationToken cancellationToken = default);
}
