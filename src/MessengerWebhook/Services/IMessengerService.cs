namespace MessengerWebhook.Services;

/// <summary>
/// Service for sending messages via Facebook Messenger Send API
/// </summary>
public interface IMessengerService
{
    /// <summary>
    /// Send a text message to a recipient
    /// </summary>
    Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text);
}

/// <summary>
/// Response from Send API
/// </summary>
public record SendMessageResponse(
    string RecipientId,
    string MessageId
);
