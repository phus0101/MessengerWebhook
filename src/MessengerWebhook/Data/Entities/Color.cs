namespace MessengerWebhook.Data.Entities;

public class Color
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string HexCode { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
