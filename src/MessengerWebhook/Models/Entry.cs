namespace MessengerWebhook.Models;

/// <summary>
/// Webhook entry containing messaging events
/// </summary>
public record Entry(
    string Id,
    long Time,
    MessagingEvent[] Messaging
);
