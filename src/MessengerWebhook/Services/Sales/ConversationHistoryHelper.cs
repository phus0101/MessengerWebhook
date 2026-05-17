using MessengerWebhook.Services.Conversation;
using MessengerWebhook.StateMachine.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.Sales;

public static class ConversationHistoryHelper
{
    public static List<AiConversationMessage> GetHistory(StateContext ctx)
        => ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();

    public static void AddToHistory(StateContext ctx, string role, string content, int limit)
    {
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
        history.Add(new AiConversationMessage { Role = role, Content = content, Timestamp = DateTime.UtcNow });
        if (history.Count > limit)
            history = history.Skip(history.Count - limit).ToList();
        ctx.SetData("conversationHistory", history);
    }

    /// <summary>
    /// Adds a message to history, then — when history exceeds <paramref name="summarizationThreshold"/> —
    /// summarizes the older turns and trims history to the ephemeral window.
    /// Summary is persisted in ctx["conversationSummary"] for injection into the system prompt.
    /// </summary>
    public static async Task AddToHistoryWithSummaryAsync(
        StateContext ctx,
        string role,
        string content,
        int historyLimit,
        int ephemeralWindowSize,
        int summarizationThreshold,
        IConversationSummarizer summarizer,
        CancellationToken ct = default)
    {
        // 1. Add to history as usual
        AddToHistory(ctx, role, content, historyLimit);

        var history = GetHistory(ctx);

        // 2. If threshold exceeded, summarize older turns and trim to ephemeral window
        if (history.Count > summarizationThreshold)
        {
            var olderTurns = history.Take(history.Count - ephemeralWindowSize).ToList();
            var existingSummary = ctx.GetData<string>("conversationSummary");
            var summary = await summarizer.SummarizeAsync(olderTurns, existingSummary, ct);
            ctx.SetData("conversationSummary", summary);

            // 3. Keep only the most recent ephemeral window in active history
            var recentTurns = history.TakeLast(ephemeralWindowSize).ToList();
            ctx.SetData("conversationHistory", recentTurns);
        }
    }
}
