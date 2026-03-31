namespace MessengerWebhook.Services.AI.Models;

/// <summary>
/// Result of AI-based customer intent detection.
/// Contains the detected intent, confidence score, and reasoning.
/// </summary>
public class IntentDetectionResult
{
    /// <summary>
    /// The detected customer intent category.
    /// </summary>
    public CustomerIntent Intent { get; set; }

    /// <summary>
    /// Confidence score from 0.0 to 1.0.
    /// Higher values indicate stronger confidence in the detected intent.
    /// Threshold: 0.7 (configurable via GeminiOptions.IntentConfidenceThreshold)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Brief explanation of why this intent was detected.
    /// Used for debugging and observability.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Detection method used: "ai-reasoning", "fallback", or "feature-disabled".
    /// Used for analytics and monitoring.
    /// </summary>
    public string DetectionMethod { get; set; } = "ai-reasoning";
}
