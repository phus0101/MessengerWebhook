namespace MessengerWebhook.Models;

/// <summary>
/// Root webhook event from Facebook
/// </summary>
public record WebhookEvent(
    string Object,
    Entry[] Entry
);
