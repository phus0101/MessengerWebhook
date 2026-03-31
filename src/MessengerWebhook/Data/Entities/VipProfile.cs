namespace MessengerWebhook.Data.Entities;

public enum VipTier
{
    Standard = 0,
    Returning = 1,
    Vip = 2
}

public class VipProfile : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid CustomerIdentityId { get; set; }
    public VipTier Tier { get; set; } = VipTier.Standard;
    public bool IsVip { get; set; }
    public int TotalOrders { get; set; }
    public decimal LifetimeValue { get; set; }
    public string GreetingStyle { get; set; } = string.Empty;
    public DateTime? LastOrderAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CustomerIdentity? CustomerIdentity { get; set; }
}
