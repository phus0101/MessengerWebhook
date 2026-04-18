using MessengerWebhook.Models;

namespace MessengerWebhook.Data.Entities;

public class ConversationSession : ITenantOwnedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid? TenantId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string? FacebookPageId { get; set; }
    public ConversationState CurrentState { get; set; } = ConversationState.Idle;
    public string? ContextJson { get; set; }
    public string? ABTestVariant { get; set; }
    public bool SurveySent { get; set; } = false;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
