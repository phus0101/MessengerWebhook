namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Complete context analysis of a conversation
/// </summary>
public class ConversationContext
{
    public JourneyStage CurrentStage { get; set; }
    public List<ConversationPattern> Patterns { get; set; } = new();
    public List<ConversationTopic> Topics { get; set; } = new();
    public ConversationQuality Quality { get; set; } = new();
    public List<ConversationInsight> Insights { get; set; } = new();
    public int TurnCount { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
