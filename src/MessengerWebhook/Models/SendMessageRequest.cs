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
