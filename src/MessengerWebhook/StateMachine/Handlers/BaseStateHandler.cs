using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public abstract class BaseStateHandler : IStateHandler
{
    protected readonly IGeminiService GeminiService;
    protected readonly ILogger Logger;

    public abstract ConversationState HandledState { get; }

    protected BaseStateHandler(
        IGeminiService geminiService,
        ILogger logger)
    {
        GeminiService = geminiService;
        Logger = logger;
    }

    public async Task<string> HandleAsync(StateContext ctx, string message)
    {
        try
        {
            Logger.LogInformation(
                "Handling state {State} for PSID: {PSID}",
                HandledState,
                ctx.FacebookPSID);

            return await HandleInternalAsync(ctx, message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling state {State} for PSID: {PSID}", HandledState, ctx.FacebookPSID);
            ctx.CurrentState = ConversationState.Error;
            return "Xin lỗi, đã có lỗi xảy ra. Vui lòng thử lại hoặc gõ 'trợ giúp' để được hỗ trợ.";
        }
    }

    protected abstract Task<string> HandleInternalAsync(StateContext ctx, string message);

    protected void AddToHistory(StateContext ctx, string role, string content)
    {
        var history = ctx.GetData<List<Services.AI.Models.ConversationMessage>>("conversationHistory")
            ?? new List<Services.AI.Models.ConversationMessage>();

        history.Add(new Services.AI.Models.ConversationMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        });

        ctx.SetData("conversationHistory", history);
    }

    protected List<Services.AI.Models.ConversationMessage> GetHistory(StateContext ctx)
    {
        return ctx.GetData<List<Services.AI.Models.ConversationMessage>>("conversationHistory")
            ?? new List<Services.AI.Models.ConversationMessage>();
    }
}
