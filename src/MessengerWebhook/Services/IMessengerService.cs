using MessengerWebhook.Models;

namespace MessengerWebhook.Services;

/// <summary>
/// Service for sending messages via Facebook Messenger Send API
/// </summary>
public interface IMessengerService
{
    /// <summary>
    /// Send a text message to a recipient
    /// </summary>
    Task<SendMessageResponse> SendTextMessageAsync(string recipientId, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a video is currently live
    /// </summary>
    Task<bool> IsVideoLiveAsync(string videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hide a comment on Facebook
    /// </summary>
    Task<bool> HideCommentAsync(string commentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reply to a comment on Facebook
    /// </summary>
    Task<bool> ReplyToCommentAsync(string commentId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a message with quick reply buttons
    /// </summary>
    Task<SendMessageResponse> SendQuickReplyAsync(
        string recipientId,
        string text,
        List<QuickReplyButton> quickReplies,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from Send API
/// </summary>
public record SendMessageResponse(
    string RecipientId,
    string MessageId
);
