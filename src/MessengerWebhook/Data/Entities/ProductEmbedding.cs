using Pgvector;

namespace MessengerWebhook.Data.Entities;

public class ProductEmbedding : ITenantOwnedEntity
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
}
