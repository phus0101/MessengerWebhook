namespace MessengerWebhook.Services.Conversation.Configuration;

/// <summary>
/// Configuration options for conversation context analysis
/// </summary>
public class ConversationAnalysisOptions
{
    /// <summary>
    /// Number of recent turns to analyze (default: 10)
    /// </summary>
    public int AnalysisWindowSize { get; set; } = 10;

    /// <summary>
    /// Enable pattern detection (repeat questions, topic shifts, etc.)
    /// </summary>
    public bool EnablePatternDetection { get; set; } = true;

    /// <summary>
    /// Enable topic extraction and analysis
    /// </summary>
    public bool EnableTopicAnalysis { get; set; } = true;

    /// <summary>
    /// Enable actionable insight generation
    /// </summary>
    public bool EnableInsightGeneration { get; set; } = true;

    /// <summary>
    /// Confidence threshold for buying signal detection (0-1)
    /// </summary>
    public double BuyingSignalThreshold { get; set; } = 0.7;

    /// <summary>
    /// Similarity threshold for repeat question detection (0-1)
    /// </summary>
    public double RepeatQuestionThreshold { get; set; } = 0.8;

    /// <summary>
    /// Number of turns to look back for repeat questions
    /// </summary>
    public int RepeatQuestionWindow { get; set; } = 5;

    /// <summary>
    /// Enable result caching
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 10;
}
