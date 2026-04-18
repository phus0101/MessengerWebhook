namespace MessengerWebhook.Services.Emotion.Configuration;

/// <summary>
/// Configuration options for emotion detection service
/// </summary>
public class EmotionDetectionOptions
{
    /// <summary>
    /// Enable context-aware emotion analysis using conversation history
    /// </summary>
    public bool EnableContextAnalysis { get; set; } = true;

    /// <summary>
    /// Number of recent messages to analyze for context (default: 3)
    /// </summary>
    public int ContextWindowSize { get; set; } = 3;

    /// <summary>
    /// Minimum confidence threshold for emotion detection (0.0 to 1.0)
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.6;

    /// <summary>
    /// Enable caching of emotion detection results
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes (default: 5)
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 5;
}
