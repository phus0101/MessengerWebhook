namespace MessengerWebhook.Data.Entities;

public enum KnowledgeCategory
{
    Product = 0,
    Gift = 1,
    Faq = 2,
    Policy = 3,
    Promotion = 4,
    Shipping = 5,
    Inventory = 6,
    Privacy = 7
}

public class KnowledgeSnapshot : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public KnowledgeCategory Category { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}
