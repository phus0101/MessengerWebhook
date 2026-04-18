using System.Text.Json;

namespace MessengerWebhook.Data.Entities;

public class ConversationMetric : ITenantOwnedEntity
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string FacebookPSID { get; set; } = string.Empty;
    public string ABTestVariant { get; set; } = string.Empty;

    // Message context
    public DateTime MessageTimestamp { get; set; }
    public int ConversationTurn { get; set; }

    // Performance metrics
    public int TotalResponseTimeMs { get; set; }
    public int? PipelineLatencyMs { get; set; }

    // Pipeline metrics (treatment only)
    public string? DetectedEmotion { get; set; }
    public decimal? EmotionConfidence { get; set; }
    public string? MatchedTone { get; set; }
    public string? JourneyStage { get; set; }
    public bool? ValidationPassed { get; set; }
    public JsonDocument? ValidationErrors { get; set; }

    // Conversation outcome
    public string? ConversationOutcome { get; set; }

    // Flexible storage
    public JsonDocument? AdditionalMetrics { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ConversationSession? Session { get; set; }
    public Tenant? Tenant { get; set; }
}
