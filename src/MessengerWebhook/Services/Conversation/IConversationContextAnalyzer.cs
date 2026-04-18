using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;

namespace MessengerWebhook.Services.Conversation;

/// <summary>
/// Interface for conversation context analysis
/// </summary>
public interface IConversationContextAnalyzer
{
    /// <summary>
    /// Analyze conversation history to extract patterns, topics, and insights
    /// </summary>
    Task<ConversationContext> AnalyzeAsync(
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze conversation with emotion history for enhanced insights
    /// </summary>
    Task<ConversationContext> AnalyzeWithEmotionAsync(
        List<ConversationMessage> history,
        List<EmotionScore> emotionHistory,
        CancellationToken cancellationToken = default);
}
