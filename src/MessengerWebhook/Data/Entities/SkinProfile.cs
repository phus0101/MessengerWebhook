using System.ComponentModel.DataAnnotations.Schema;

namespace MessengerWebhook.Data.Entities;

public class SkinProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;

    public string SkinType { get; set; } = string.Empty;  // oily, dry, combination, sensitive

    [Column(TypeName = "jsonb")]
    public string? ConcernsJson { get; set; }  // List<string>: acne, aging, dryness

    [Column(TypeName = "jsonb")]
    public string? SensitivitiesJson { get; set; }  // List<string>: fragrance, alcohol

    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ConversationSession Session { get; set; } = null!;
}
