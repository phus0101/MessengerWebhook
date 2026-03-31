using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class ErrorStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Error;

    public ErrorStateHandler(
        IGeminiService geminiService,
        ILogger<ErrorStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        // Reset to Idle state on any message
        ctx.CurrentState = ConversationState.Idle;

        Logger.LogInformation("Error state reset to Idle for PSID: {PSID}", ctx.FacebookPSID);

        return "Xin lỗi, đã có lỗi xảy ra. Hãy thử lại nhé! 🌸";
    }
}
