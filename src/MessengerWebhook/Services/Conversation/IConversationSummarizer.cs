using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.Conversation;

public interface IConversationSummarizer
{
    Task<string> SummarizeAsync(
        IReadOnlyList<ConversationMessage> olderTurns,
        string? existingSummary,
        CancellationToken ct = default);
}
