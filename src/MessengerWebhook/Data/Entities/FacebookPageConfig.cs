namespace MessengerWebhook.Data.Entities;

public class FacebookPageConfig : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string FacebookPageId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string? PageAccessToken { get; set; }
    public string? VerifyToken { get; set; }
    public string? AppSecretOverride { get; set; }
    public string? DefaultManagerEmail { get; set; }
    public bool IsPrimaryPage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}
