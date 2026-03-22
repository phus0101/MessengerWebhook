using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class IdleStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Idle;

    public IdleStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<IdleStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        await TransitionToAsync(ctx, ConversationState.Greeting);
        return "Welcome to our cosmetics store! 🌸";
    }
}
