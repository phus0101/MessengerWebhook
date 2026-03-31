namespace MessengerWebhook.Data.Entities;

public class CustomerIdentity : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string? FacebookPageId { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ShippingAddress { get; set; }
    public int TotalOrders { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public decimal LifetimeValue { get; set; }
    public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public VipProfile? VipProfile { get; set; }
    public ICollection<RiskSignal> RiskSignals { get; set; } = new List<RiskSignal>();
}
