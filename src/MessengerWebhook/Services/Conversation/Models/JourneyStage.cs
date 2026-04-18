namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Represents the customer's position in the buying journey
/// </summary>
public enum JourneyStage
{
    /// <summary>
    /// Customer is browsing, asking general questions
    /// </summary>
    Browsing,

    /// <summary>
    /// Customer is considering purchase, comparing options
    /// </summary>
    Considering,

    /// <summary>
    /// Customer is ready to buy, showing strong purchase intent
    /// </summary>
    Ready,

    /// <summary>
    /// Customer has stalled, needs gentle nudge
    /// </summary>
    Stalled,

    /// <summary>
    /// Customer has abandoned conversation
    /// </summary>
    Abandoned
}
