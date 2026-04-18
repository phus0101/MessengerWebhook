namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Represents a detected pattern in conversation history
/// </summary>
public class ConversationPattern
{
    public PatternType Type { get; set; }
    public int Occurrences { get; set; }
    public List<int> TurnIndices { get; set; } = new();
    public double Confidence { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Types of conversation patterns that can be detected
/// </summary>
public enum PatternType
{
    /// <summary>
    /// Customer asking the same question again
    /// </summary>
    RepeatQuestion,

    /// <summary>
    /// Sudden topic change in conversation
    /// </summary>
    TopicShift,

    /// <summary>
    /// Customer showing purchase intent
    /// </summary>
    BuyingSignal,

    /// <summary>
    /// Customer showing hesitation or uncertainty
    /// </summary>
    Hesitation,

    /// <summary>
    /// Customer sensitive about pricing
    /// </summary>
    PriceSensitivity,

    /// <summary>
    /// Customer engagement decreasing
    /// </summary>
    EngagementDrop,

    /// <summary>
    /// Missing information causing confusion
    /// </summary>
    InformationGap
}
