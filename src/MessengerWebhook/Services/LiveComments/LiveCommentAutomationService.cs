namespace MessengerWebhook.Services.LiveComments;

public class LiveCommentAutomationService : ILiveCommentAutomationService
{
    public Task<bool> ShouldHandleCommentAsync(string commentText, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(commentText));
    }
}
