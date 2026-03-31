using System.Text.Json.Serialization;

namespace MessengerWebhook.Models;

public class FeedChangeValue
{
    [JsonPropertyName("item")]
    public string Item { get; set; } = string.Empty;

    [JsonPropertyName("comment_id")]
    public string CommentId { get; set; } = string.Empty;

    [JsonPropertyName("post_id")]
    public string PostId { get; set; } = string.Empty;

    [JsonPropertyName("verb")]
    public string Verb { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public FeedUser From { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("created_time")]
    public long CreatedTime { get; set; }
}
