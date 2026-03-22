using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public abstract class BaseStateHandler : IStateHandler
{
    protected readonly IGeminiService GeminiService;
    protected readonly IStateMachine StateMachine;
    protected readonly ILogger Logger;

    public abstract ConversationState HandledState { get; }

    protected BaseStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger logger)
    {
        GeminiService = geminiService;
        StateMachine = stateMachine;
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

            var response = await HandleInternalAsync(ctx, message);
            await StateMachine.SaveAsync(ctx);
            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling state {State} for PSID: {PSID}", HandledState, ctx.FacebookPSID);
            await TransitionToAsync(ctx, ConversationState.Error);
            await StateMachine.SaveAsync(ctx);
            return "Sorry, something went wrong. Please try again or type 'help' for assistance.";
        }
    }

    protected abstract Task<string> HandleInternalAsync(StateContext ctx, string message);

    protected async Task<bool> TransitionToAsync(StateContext ctx, ConversationState newState)
    {
        return await StateMachine.TransitionToAsync(ctx, newState);
    }

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
