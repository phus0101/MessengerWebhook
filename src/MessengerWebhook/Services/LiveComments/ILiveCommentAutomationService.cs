namespace MessengerWebhook.Services.LiveComments;

public interface ILiveCommentAutomationService
{
    Task<bool> ShouldHandleCommentAsync(string commentText, CancellationToken cancellationToken = default);
}
