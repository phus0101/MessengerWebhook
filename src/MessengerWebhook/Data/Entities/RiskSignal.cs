namespace MessengerWebhook.Data.Entities;

public class RiskSignal : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? CustomerIdentityId { get; set; }
    public Guid? DraftOrderId { get; set; }
    public decimal Score { get; set; }
    public RiskLevel Level { get; set; } = RiskLevel.Low;
    public string Source { get; set; } = "local";
    public string Reason { get; set; } = string.Empty;
    public string CustomerMessage { get; set; } = string.Empty;
    public bool RequiresManualReview { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CustomerIdentity? CustomerIdentity { get; set; }
    public DraftOrder? DraftOrder { get; set; }
}
