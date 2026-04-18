namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Actionable insight derived from conversation analysis
/// </summary>
public class ConversationInsight
{
    public InsightType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? SuggestedAction { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Types of actionable insights
/// </summary>
public enum InsightType
{
    /// <summary>
    /// Suggest offering a discount
    /// </summary>
    SuggestDiscount,

    /// <summary>
    /// Escalate to human agent
    /// </summary>
    EscalateToHuman,

    /// <summary>
    /// Customer ready to close sale
    /// </summary>
    CloseSale,

    /// <summary>
    /// Provide more product information
    /// </summary>
    ProvideMoreInfo,

    /// <summary>
    /// Give customer time to think
    /// </summary>
    GiveSpace,

    /// <summary>
    /// Address customer objection
    /// </summary>
    AddressObjection
}
