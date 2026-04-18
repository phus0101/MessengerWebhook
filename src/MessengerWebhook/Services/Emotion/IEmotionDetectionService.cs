using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.Emotion;

/// <summary>
/// Service interface for emotion detection
/// </summary>
public interface IEmotionDetectionService
{
    /// <summary>
    /// Detect emotion from a single message
    /// </summary>
    Task<EmotionScore> DetectEmotionAsync(
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect emotion with conversation context
    /// </summary>
    Task<EmotionScore> DetectEmotionWithContextAsync(
        string message,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);
}
