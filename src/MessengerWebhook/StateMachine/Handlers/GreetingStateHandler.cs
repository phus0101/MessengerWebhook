using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class GreetingStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Greeting;

    public GreetingStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<GreetingStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var prompt = $@"User said: '{message}'
Detect intent: greeting, browse_products, skin_analysis, order_tracking, help, or other.
Respond with ONLY the intent name.";

        var history = GetHistory(ctx);
        var intent = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        intent = intent.Trim().ToLowerInvariant();

        Logger.LogInformation("Detected intent: {Intent} for PSID: {PSID}", intent, ctx.FacebookPSID);

        var response = "Hello! I'm here to help you find the perfect cosmetics. ";

        if (intent.Contains("skin") || intent.Contains("analysis"))
        {
            await TransitionToAsync(ctx, ConversationState.SkinConsultation);
            response += "Let's start with a skin consultation!";
        }
        else if (intent.Contains("track") || intent.Contains("order"))
        {
            await TransitionToAsync(ctx, ConversationState.OrderTracking);
            response += "I can help you track your order.";
        }
        else
        {
            await TransitionToAsync(ctx, ConversationState.MainMenu);
            response += "What would you like to do?\n\n1. Browse products\n2. Skin consultation\n3. Track order\n4. Help";
        }

        AddToHistory(ctx, "model", response);
        return response;
    }
}
