namespace MessengerWebhook.Services.QuickReply;

/// <summary>
/// Handler for Quick Reply and Postback events from Facebook Messenger
/// </summary>
public interface IQuickReplyHandler
{
    /// <summary>
    /// Handle Quick Reply event
    /// </summary>
    Task<string> HandleQuickReplyAsync(string senderId, string payload);

    /// <summary>
    /// Handle Postback event
    /// </summary>
    Task<string> HandlePostbackAsync(string senderId, string payload);
}
