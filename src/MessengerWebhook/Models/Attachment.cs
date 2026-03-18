namespace MessengerWebhook.Models;

/// <summary>
/// Message attachment (image, video, file, etc.)
/// </summary>
public record Attachment(
    string Type,
    AttachmentPayload? Payload
);

public record AttachmentPayload(
    string? Url
);
