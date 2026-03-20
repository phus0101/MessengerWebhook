namespace MessengerWebhook.Data.Entities;

public class Cart
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Navigation properties
    public ConversationSession Session { get; set; } = null!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
