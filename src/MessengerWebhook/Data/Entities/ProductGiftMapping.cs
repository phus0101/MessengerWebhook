using System.ComponentModel.DataAnnotations;

namespace MessengerWebhook.Data.Entities;

/// <summary>
/// Mapping between products and gifts for promotions
/// </summary>
public class ProductGiftMapping
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string GiftCode { get; set; } = string.Empty;

    public int Priority { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product? Product { get; set; }
    public Gift? Gift { get; set; }
}
