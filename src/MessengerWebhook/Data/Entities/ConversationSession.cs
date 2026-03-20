namespace MessengerWebhook.Data.Entities;

public enum ConversationState
{
    Idle,
    Browsing,
    ProductSelected,
    SizeSelection,
    ColorSelection,
    CartReview,
    AddressInput,
    OrderReview,
    OrderConfirmed
}

public class ConversationSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FacebookPSID { get; set; } = string.Empty;
    public ConversationState CurrentState { get; set; } = ConversationState.Idle;
    public string? ContextJson { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
