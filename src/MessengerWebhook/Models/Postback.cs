namespace MessengerWebhook.Models;

/// <summary>
/// Postback event from button click
/// </summary>
public record Postback(
    string Title,
    string Payload
);
