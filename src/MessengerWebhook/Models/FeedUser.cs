using System.Text.Json.Serialization;

namespace MessengerWebhook.Models;

public class FeedUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
