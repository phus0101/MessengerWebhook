using System.Text.Json.Serialization;

namespace MessengerWebhook.Models;

/// <summary>
/// Request to Facebook Send API
/// </summary>
public record SendMessageRequest(
    SendRecipient Recipient,
    SendMessage Message
);

public record SendRecipient(string Id);

public record SendMessage(string Text);

/// <summary>
/// Quick Reply button for outgoing messages (Facebook Send API)
/// </summary>
public record QuickReplyButton(
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("payload")] string Payload
);

/// <summary>
/// Message with Quick Reply buttons
/// </summary>
public record SendMessageWithQuickReplies(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("quick_replies")] List<QuickReplyButton> QuickReplies
);

/// <summary>
/// Request to send message with Quick Reply buttons
/// </summary>
public record SendQuickReplyRequest(
    [property: JsonPropertyName("recipient")] SendRecipient Recipient,
    [property: JsonPropertyName("message")] SendMessageWithQuickReplies Message
);
