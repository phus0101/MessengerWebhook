namespace MessengerWebhook.Services.AI.Models;

/// <summary>
/// Result of AI-based confirmation detection for customer messages.
/// Used to determine if a customer is confirming remembered contact information.
/// </summary>
public class ConfirmationDetectionResult
{
    /// <summary>
    /// Whether the customer message is confirming the remembered contact info.
    /// </summary>
    public bool IsConfirming { get; set; }

    /// <summary>
    /// Confidence score from 0.0 to 1.0 indicating how certain the AI is about the classification.
    /// Threshold of 0.7 is recommended for production use.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Brief explanation in English of why the AI made this classification.
    /// Useful for debugging and monitoring false positives/negatives.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Detection method used: "explicit-data" (fast path), "ai-reasoning", or "fallback".
    /// </summary>
    public string DetectionMethod { get; set; } = "ai-reasoning";
}
