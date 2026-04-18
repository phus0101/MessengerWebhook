namespace MessengerWebhook.Services.Conversation.Models;

/// <summary>
/// Represents a topic discussed in the conversation
/// </summary>
public class ConversationTopic
{
    public string Name { get; set; } = string.Empty;
    public int MentionCount { get; set; }
    public double Relevance { get; set; }  // 0-1
    public List<string> Keywords { get; set; } = new();
}
