namespace MessengerWebhook.Services.Emotion.Models;

/// <summary>
/// Represents the emotion detection result with confidence scores
/// </summary>
public class EmotionScore
{
    /// <summary>
    /// The primary detected emotion type
    /// </summary>
    public EmotionType PrimaryEmotion { get; set; }

    /// <summary>
    /// Confidence scores for each emotion type (0.0 to 1.0)
    /// </summary>
    public Dictionary<EmotionType, double> Scores { get; set; } = new();

    /// <summary>
    /// Overall confidence in the detection (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Detection method used: "rule-based" or "ml"
    /// </summary>
    public string DetectionMethod { get; set; } = "rule-based";

    /// <summary>
    /// Additional metadata about the detection
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
