using System.ComponentModel.DataAnnotations.Schema;

namespace MessengerWebhook.Data.Entities;

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public ProductCategory Category { get; set; } = ProductCategory.Cosmetics;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;

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
