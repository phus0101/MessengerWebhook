using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.SmallTalk;

/// <summary>
/// Service for detecting small talk and generating natural responses
/// </summary>
public interface ISmallTalkService
{
    /// <summary>
    /// Analyzes message for small talk intent and generates appropriate response
    /// </summary>
    Task<SmallTalkResponse> AnalyzeAsync(
        SmallTalkContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes message for small talk intent (convenience overload)
    /// </summary>
    Task<SmallTalkResponse> AnalyzeAsync(
        string message,
        EmotionScore emotion,
        ToneProfile toneProfile,
        ConversationContext conversationContext,
        VipProfile vipProfile,
        bool isReturningCustomer,
        int conversationTurnCount,
        CancellationToken cancellationToken = default);
}
