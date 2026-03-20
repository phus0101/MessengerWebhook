namespace MessengerWebhook.Data.Entities;

public class Size
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // men, women, unisex

    // Navigation properties
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
