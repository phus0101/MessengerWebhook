namespace MessengerWebhook.Configuration;

/// <summary>
/// Configuration options for sub-intent classification system
/// </summary>
public class SubIntentOptions
{
    public const string SectionName = "SubIntent";

    /// <summary>Timeout for AI classifier in milliseconds (default: 500ms)</summary>
    public int ClassifierTimeoutMs { get; set; } = 500;

    /// <summary>Minimum confidence threshold for AI classification (default: 0.5)</summary>
    public decimal MinConfidence { get; set; } = 0.5m;

    /// <summary>Confidence threshold for keyword high-confidence acceptance (default: 0.9)</summary>
    public decimal KeywordHighConfidenceThreshold { get; set; } = 0.9m;

    /// <summary>Confidence threshold for AI result acceptance in hybrid mode (default: 0.7)</summary>
    public decimal HybridAiAcceptanceThreshold { get; set; } = 0.7m;

    /// <summary>Enable AI fallback classifier (default: true)</summary>
    public bool EnableAiFallback { get; set; } = true;
}
