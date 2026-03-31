namespace MessengerWebhook.Models;

/// <summary>
/// Represents the current state of a conversation in the sales flow.
/// Separated into its own namespace to avoid conflicts with other domain models.
/// </summary>
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
    QuickReplySales = 101,
    Consulting = 102,
    CollectingInfo = 103,
    DraftOrder = 104,
    Complete = 105,
    HumanHandoff = 106,
    Error = 99
}
