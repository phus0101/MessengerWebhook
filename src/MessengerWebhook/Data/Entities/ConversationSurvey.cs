namespace MessengerWebhook.Data.Entities;

public class ConversationSurvey : ITenantOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string FacebookPsid { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }

    // A/B Test Context
    public string? ABTestVariant { get; set; }

    // Survey Data
    public int Rating { get; set; } // 1-5
    public string? FeedbackText { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ConversationSession? Session { get; set; }
}
