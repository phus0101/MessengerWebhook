namespace MessengerWebhook.Services.Metrics.Models;

public class ConversationMetricData
{
    public required string SessionId { get; init; }
    public required string FacebookPSID { get; init; }
    public required string ABTestVariant { get; init; }

    // Message context
    public required DateTime MessageTimestamp { get; init; }
    public required int ConversationTurn { get; init; }

    // Performance metrics
    public required int TotalResponseTimeMs { get; init; }
    public int? PipelineLatencyMs { get; init; }

    // Pipeline metrics (treatment only)
    public string? DetectedEmotion { get; init; }
    public decimal? EmotionConfidence { get; init; }
    public string? MatchedTone { get; init; }
    public string? JourneyStage { get; init; }
    public bool? ValidationPassed { get; init; }
    public Dictionary<string, object>? ValidationErrors { get; init; }

    // Conversation outcome
    public string? ConversationOutcome { get; init; }

    // Flexible storage
    public Dictionary<string, object>? AdditionalMetrics { get; init; }

    // Retry tracking for failed flushes
    public int RetryCount { get; set; }
}
