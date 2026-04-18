namespace MessengerWebhook.Services.Tone.Configuration;

/// <summary>
/// Configuration options for tone matching service
/// </summary>
public class ToneMatchingOptions
{
    /// <summary>
    /// Enable emotion-based tone adaptation
    /// </summary>
    public bool EnableEmotionBasedAdaptation { get; set; } = true;

    /// <summary>
    /// Enable escalation detection for frustrated customers
    /// </summary>
    public bool EnableEscalationDetection { get; set; } = true;

    /// <summary>
    /// Confidence threshold for frustration escalation (0.0 to 1.0)
    /// </summary>
    public double FrustrationEscalationThreshold { get; set; } = 0.7;

    /// <summary>
    /// Default pronoun when uncertain: "anh", "chị", "em", "bạn"
    /// </summary>
    public string DefaultPronoun { get; set; } = "bạn";

    /// <summary>
    /// Enable caching of tone profiles
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 5;
}
