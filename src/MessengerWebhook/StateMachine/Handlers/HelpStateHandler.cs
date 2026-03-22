using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class HelpStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Help;

    public HelpStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<HelpStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var prompt = $@"User asked for help: '{message}'
Provide a brief, helpful response about using the cosmetics chatbot.
Topics: browsing products, skin consultation, cart, checkout, order tracking.
Keep response under 100 words.";

        var history = GetHistory(ctx);
        var helpResponse = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

        // Return to previous state or main menu
        var previousState = ctx.GetData<ConversationState?>("previousState");
        if (previousState.HasValue && previousState.Value != ConversationState.Help)
        {
            await TransitionToAsync(ctx, previousState.Value);
            helpResponse += "\n\nReturning to where you were...";
        }
        else
        {
            await TransitionToAsync(ctx, ConversationState.MainMenu);
            helpResponse += "\n\nReturning to main menu.";
        }

        AddToHistory(ctx, "model", helpResponse);
        return helpResponse;
    }
}
