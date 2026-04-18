namespace MessengerWebhook.Services.SmallTalk.Configuration;

/// <summary>
/// Configuration options for small talk detection and response generation
/// </summary>
public class SmallTalkOptions
{
    /// <summary>
    /// Enable small talk detection feature
    /// </summary>
    public bool EnableSmallTalkDetection { get; set; } = true;

    /// <summary>
    /// Enable context-aware greetings (time of day, VIP, returning customer)
    /// </summary>
    public bool EnableContextAwareGreetings { get; set; } = true;

    /// <summary>
    /// Minimum confidence threshold for small talk classification (0.0 - 1.0)
    /// </summary>
    public double SmallTalkConfidenceThreshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of small talk turns before offering help
    /// </summary>
    public int MaxSmallTalkTurns { get; set; } = 3;

    /// <summary>
    /// Enable soft transitions ("Có gì em giúp không?") instead of direct business push
    /// </summary>
    public bool EnableSoftTransitions { get; set; } = true;
}
