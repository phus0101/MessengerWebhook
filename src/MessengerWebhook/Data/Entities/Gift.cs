using System.ComponentModel.DataAnnotations;

namespace MessengerWebhook.Data.Entities;

/// <summary>
/// Gift entity for product promotions
/// </summary>
public class Gift
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ProductGiftMapping> ProductGiftMappings { get; set; } = new List<ProductGiftMapping>();
}
