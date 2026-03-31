namespace MessengerWebhook.Data.Entities;

public class AdminAuditLog : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string? FacebookPageId { get; set; }
    public Guid? ManagerProfileId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
