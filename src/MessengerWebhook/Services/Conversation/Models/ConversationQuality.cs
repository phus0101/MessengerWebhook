namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Metrics representing conversation quality
/// </summary>
public class ConversationQuality
{
    /// <summary>
    /// Overall quality score (0-100)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// How well the conversation flows
    /// </summary>
    public double Coherence { get; set; }

    /// <summary>
    /// Level of customer participation
    /// </summary>
    public double Engagement { get; set; }

    /// <summary>
    /// Whether engagement is increasing or decreasing
    /// </summary>
    public double Momentum { get; set; }

    /// <summary>
    /// Additional quality metrics
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();
}
