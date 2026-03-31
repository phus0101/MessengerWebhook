namespace MessengerWebhook.Services.LiveComments;

public interface ILiveCommentAutomationService
{
    Task<bool> ShouldHandleCommentAsync(string commentText, CancellationToken cancellationToken = default);

    Task ProcessCommentAsync(
        string commentId,
        string commenterPsid,
        string commentText,
        string videoId,
        CancellationToken cancellationToken = default);
}
