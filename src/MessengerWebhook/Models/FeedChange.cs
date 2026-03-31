using System.Text.Json.Serialization;

namespace MessengerWebhook.Models;

public class FeedChange
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public FeedChangeValue? Value { get; set; }
}
