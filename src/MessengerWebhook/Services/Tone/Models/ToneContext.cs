using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Emotion.Models;

namespace MessengerWebhook.Services.Tone.Models;

/// <summary>
/// Aggregates all context needed for tone matching
/// </summary>
public class ToneContext
{
    /// <summary>
    /// The detected emotion from the customer's message
    /// </summary>
    public EmotionScore Emotion { get; set; } = null!;

    /// <summary>
    /// The customer's VIP profile
    /// </summary>
    public VipProfile VipProfile { get; set; } = null!;

    /// <summary>
    /// The customer's identity and history
    /// </summary>
    public CustomerIdentity Customer { get; set; } = null!;

    /// <summary>
    /// Number of turns in the current conversation
    /// </summary>
    public int ConversationTurnCount { get; set; }

    /// <summary>
    /// Whether this is the first interaction with the customer
    /// </summary>
    public bool IsFirstInteraction { get; set; }
}
