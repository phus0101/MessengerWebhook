using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.AI;

public interface IGeminiService
{
    Task<string> SendMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string userId,
        string message,
        List<ConversationMessage> history,
        GeminiModelType? modelOverride = null,
        CancellationToken cancellationToken = default);

    GeminiModelType SelectModel(string message);
}
