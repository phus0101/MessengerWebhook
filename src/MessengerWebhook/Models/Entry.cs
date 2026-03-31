using System.Text.Json.Serialization;

namespace MessengerWebhook.Models;

/// <summary>
/// Webhook entry containing messaging events or feed changes
/// </summary>
public record Entry(
    string Id,
    long Time,
    [property: JsonPropertyName("messaging")] MessagingEvent[]? Messaging,
    [property: JsonPropertyName("changes")] FeedChange[]? Changes
);
