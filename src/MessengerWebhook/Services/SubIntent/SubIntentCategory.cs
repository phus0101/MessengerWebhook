namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Sub-intent categories for customer questions in sales conversations
/// </summary>
public enum SubIntentCategory
{
    /// <summary>No specific sub-intent detected</summary>
    None = 0,

    /// <summary>Questions about product features, ingredients, usage</summary>
    ProductQuestion = 1,

    /// <summary>Questions about price, cost, discounts</summary>
    PriceQuestion = 2,

    /// <summary>Questions about delivery time, shipping cost, tracking</summary>
    ShippingQuestion = 3,

    /// <summary>Questions about return, refund, warranty policies</summary>
    PolicyQuestion = 4,

    /// <summary>Questions about stock availability</summary>
    AvailabilityQuestion = 5,

    /// <summary>Comparing multiple products</summary>
    ComparisonQuestion = 6
}
