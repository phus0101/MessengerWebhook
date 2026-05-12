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
}
