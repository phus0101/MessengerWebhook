namespace MessengerWebhook.Data.Entities;

public class ProductVariant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string ColorId { get; set; } = string.Empty;
    public string SizeId { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public Color Color { get; set; } = null!;
    public Size Size { get; set; } = null!;
}
