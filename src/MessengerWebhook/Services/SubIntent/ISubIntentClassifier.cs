namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Classifies customer messages into sub-intent categories
/// </summary>
public interface ISubIntentClassifier
{
    /// <summary>
    /// Classify a customer message into a sub-intent category
    /// </summary>
    /// <param name="message">Customer message text</param>
    /// <param name="conversationContext">Optional conversation context for disambiguation</param>
    /// <param name="cancellationToken">Cancellation token for timeout</param>
    /// <returns>Classification result or null if unable to classify</returns>
    Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Conversation context for intent classification
/// </summary>
public sealed record ConversationContext
{
    /// <summary>Current conversation state</summary>
    public string? CurrentState { get; init; }

    /// <summary>Whether customer has selected a product</summary>
    public bool HasProduct { get; init; }

    /// <summary>Recent conversation history (last 3-5 messages)</summary>
    public List<ConversationMessage> RecentHistory { get; init; } = new();

    /// <summary>Dominant topic from TopicAnalyzer</summary>
    public string? DominantTopic { get; init; }
}

/// <summary>
/// Conversation message for context
/// </summary>
public sealed record ConversationMessage
{
    public required string Role { get; init; } // "user" or "assistant"
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
