namespace MessengerWebhook.Services.Tenants;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? FacebookPageId { get; }
    string? ManagerEmail { get; }
    bool IsResolved { get; }
    void Initialize(Guid? tenantId, string? facebookPageId, string? managerEmail);
    void Clear();
}
