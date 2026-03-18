namespace MessengerWebhook.Models;

/// <summary>
/// Message content in messaging event
/// </summary>
public record Message(
    string Mid,
    string? Text,
    Attachment[]? Attachments
);
