namespace MessengerWebhook.Models;

/// <summary>
/// Webhook verification request from Facebook
/// </summary>
public record WebhookVerificationRequest(
    string Mode,
    string VerifyToken,
    string Challenge
);
