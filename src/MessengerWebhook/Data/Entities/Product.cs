using System.ComponentModel.DataAnnotations.Schema;

namespace MessengerWebhook.Data.Entities;

public class Product : ITenantOwnedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public ProductCategory Category { get; set; } = ProductCategory.Cosmetics;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int? NobitaProductId { get; set; }
    public decimal NobitaWeight { get; set; }
    public DateTime? NobitaLastSyncedAt { get; set; }
    public string? NobitaSyncError { get; set; }

    // Cosmetics-specific fields (JSON columns for PostgreSQL)
    [Column(TypeName = "jsonb")]
    public string? IngredientsJson { get; set; }

    [Column(TypeName = "jsonb")]
    public string? SkinTypesJson { get; set; }

    [Column(TypeName = "jsonb")]
    public string? SkinConcernsJson { get; set; }

    public double? pH { get; set; }
    public string? Texture { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ContraindicationsJson { get; set; }

    // RAG: Vector embedding for semantic search (768 dimensions for text-embedding-004)
    // Stored as vector(768) in PostgreSQL, handled via raw SQL in repository
    [NotMapped]
    public float[]? Embedding { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}

public enum ProductCategory
{
    Cosmetics,
    Fashion,
    Electronics
}
