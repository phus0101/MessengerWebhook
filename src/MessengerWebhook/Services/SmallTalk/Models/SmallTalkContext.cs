using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Emotion.Models;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.SmallTalk.Models;

/// <summary>
/// Context information for small talk analysis
/// </summary>
public class SmallTalkContext
{
    /// <summary>
    /// User message text
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detected emotion from EmotionDetectionService
    /// </summary>
    public EmotionScore Emotion { get; set; } = null!;

    /// <summary>
    /// Tone profile from ToneMatchingService
    /// </summary>
    public ToneProfile ToneProfile { get; set; } = null!;

    /// <summary>
    /// Conversation context from ConversationContextAnalyzer
    /// </summary>
    public ConversationContext ConversationContext { get; set; } = null!;

    /// <summary>
    /// VIP profile for personalization
    /// </summary>
    public VipProfile VipProfile { get; set; } = null!;

    /// <summary>
    /// Whether customer has previous orders
    /// </summary>
    public bool IsReturningCustomer { get; set; }

    /// <summary>
    /// Number of conversation turns so far
    /// </summary>
    public int ConversationTurnCount { get; set; }

    /// <summary>
    /// Current time of day for context-aware greetings
    /// </summary>
    public TimeOfDay TimeOfDay { get; set; }
}

/// <summary>
/// Time of day for context-aware greetings
/// </summary>
public enum TimeOfDay
{
    /// <summary>
    /// 5am - 12pm
    /// </summary>
    Morning,

    /// <summary>
    /// 12pm - 6pm
    /// </summary>
    Afternoon,

    /// <summary>
    /// 6pm - 5am
    /// </summary>
    Evening
}
