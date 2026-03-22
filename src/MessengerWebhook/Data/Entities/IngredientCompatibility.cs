namespace MessengerWebhook.Data.Entities;

public class IngredientCompatibility
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Ingredient1 { get; set; } = string.Empty;  // e.g., "Retinol"
    public string Ingredient2 { get; set; } = string.Empty;  // e.g., "AHA"
    public CompatibilityType Type { get; set; }
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum CompatibilityType
{
    Contraindicated,  // Should NOT use together
    Caution,          // Can use but with care
    Synergistic       // Work well together
}
