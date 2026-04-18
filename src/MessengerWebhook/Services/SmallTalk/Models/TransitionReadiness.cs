namespace MessengerWebhook.Services.SmallTalk.Models;

/// <summary>
/// Indicates readiness to transition from small talk to business conversation
/// </summary>
public enum TransitionReadiness
{
    /// <summary>
    /// Continue casual conversation, no business push
    /// </summary>
    StayInSmallTalk,

    /// <summary>
    /// Offer help gently: "Có gì em giúp được không?"
    /// </summary>
    SoftOffer,

    /// <summary>
    /// Customer showing buying signals, ready for business
    /// </summary>
    ReadyForBusiness
}
