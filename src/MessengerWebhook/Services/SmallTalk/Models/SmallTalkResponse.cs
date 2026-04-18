namespace MessengerWebhook.Services.SmallTalk.Models;

/// <summary>
/// Response from small talk analysis containing intent, suggested response, and transition readiness
/// </summary>
public class SmallTalkResponse
{
    /// <summary>
    /// Detected small talk intent
    /// </summary>
    public SmallTalkIntent Intent { get; set; }

    /// <summary>
    /// Whether message is classified as small talk
    /// </summary>
    public bool IsSmallTalk { get; set; }

    /// <summary>
    /// Suggested response text (optional, can be null if AI should generate)
    /// </summary>
    public string? SuggestedResponse { get; set; }

    /// <summary>
    /// Readiness to transition to business conversation
    /// </summary>
    public TransitionReadiness TransitionReadiness { get; set; }

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Additional metadata (time of day, returning customer, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
