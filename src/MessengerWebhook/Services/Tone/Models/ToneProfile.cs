namespace MessengerWebhook.Services.Tone.Models;

/// <summary>
/// Represents the tone adaptation profile for a customer interaction
/// </summary>
public class ToneProfile
{
    /// <summary>
    /// The formality level to use in responses
    /// </summary>
    public ToneLevel Level { get; set; }

    /// <summary>
    /// The Vietnamese pronoun to use when addressing the customer
    /// </summary>
    public VietnamesePronoun Pronoun { get; set; }

    /// <summary>
    /// The pronoun as text: "anh", "chị", "em", "bạn"
    /// </summary>
    public string PronounText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this interaction requires escalation to human agent
    /// </summary>
    public bool RequiresEscalation { get; set; }

    /// <summary>
    /// Reason for escalation if RequiresEscalation is true
    /// </summary>
    public string? EscalationReason { get; set; }

    /// <summary>
    /// Tone instructions to inject into AI prompt
    /// Keys: "tone_level", "emotion_adaptation", "escalation"
    /// </summary>
    public Dictionary<string, string> ToneInstructions { get; set; } = new();

    /// <summary>
    /// Additional metadata about the tone profile
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
