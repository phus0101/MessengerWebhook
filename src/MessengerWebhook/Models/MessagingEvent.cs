namespace MessengerWebhook.Models;

/// <summary>
/// Individual messaging event (message, postback, etc.)
/// </summary>
public record MessagingEvent(
    Sender Sender,
    Recipient Recipient,
    long Timestamp,
    Message? Message,
    Postback? Postback
);
