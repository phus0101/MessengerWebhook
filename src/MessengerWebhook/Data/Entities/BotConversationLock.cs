namespace MessengerWebhook.Data.Entities;

public class BotConversationLock : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? HumanSupportCaseId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string? FacebookPageId { get; set; }
    public bool IsLocked { get; set; } = true;
    public string Reason { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnlockAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}
