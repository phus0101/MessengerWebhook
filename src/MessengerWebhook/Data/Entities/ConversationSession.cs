namespace MessengerWebhook.Data.Entities;

public enum ConversationState
{
    Idle = 0,
    Greeting = 1,
    MainMenu = 2,
    BrowsingProducts = 3,
    ProductDetail = 4,
    SkinAnalysis = 5,
    VariantSelection = 6,
    AddToCart = 7,
    CartReview = 8,
    ShippingAddress = 9,
    PaymentMethod = 10,
    OrderConfirmation = 11,
    OrderPlaced = 12,
    OrderTracking = 20,
    SkinConsultation = 21,
    Help = 30,
    Error = 99
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
