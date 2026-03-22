using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class IdleStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Idle;

    public IdleStateHandler(
        IGeminiService geminiService,
        ILogger<IdleStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        ctx.CurrentState = ConversationState.Greeting;
        return "Welcome to our cosmetics store! 🌸";
    }
}
