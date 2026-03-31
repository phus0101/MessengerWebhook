namespace MessengerWebhook.Data.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FacebookPageConfig> FacebookPages { get; set; } = new List<FacebookPageConfig>();
    public ICollection<ManagerProfile> Managers { get; set; } = new List<ManagerProfile>();
}
