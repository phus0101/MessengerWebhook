namespace MessengerWebhook.Data.Entities;

public class CartItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CartId { get; set; } = string.Empty;
    public string VariantId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Cart Cart { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
