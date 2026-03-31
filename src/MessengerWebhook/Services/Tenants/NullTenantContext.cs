namespace MessengerWebhook.Services.Tenants;

public sealed class NullTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? FacebookPageId { get; private set; }
    public string? ManagerEmail { get; private set; }
    public bool IsResolved => TenantId.HasValue;

    public void Initialize(Guid? tenantId, string? facebookPageId, string? managerEmail)
    {
        TenantId = tenantId;
        FacebookPageId = facebookPageId;
        ManagerEmail = managerEmail;
    }

    public void Clear()
    {
        TenantId = null;
        FacebookPageId = null;
        ManagerEmail = null;
    }
}
