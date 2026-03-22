using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class MainMenuStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.MainMenu;

    public MainMenuStateHandler(
        IGeminiService geminiService,
        ILogger<MainMenuStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var prompt = $@"User said: '{message}'
Detect intent: browse_products, skin_consultation, order_tracking, help, or other.
Respond with ONLY the intent name.";

        var history = GetHistory(ctx);
        var intent = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        intent = intent.Trim().ToLowerInvariant();

        Logger.LogInformation("Main menu intent: {Intent} for PSID: {PSID}", intent, ctx.FacebookPSID);

        string response;

        if (intent.Contains("browse") || intent.Contains("product") || intent.Contains("1"))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            response = "Great! What type of products are you looking for? (e.g., moisturizer, serum, cleanser)";
        }
        else if (intent.Contains("skin") || intent.Contains("consultation") || intent.Contains("2"))
        {
            ctx.CurrentState = ConversationState.SkinConsultation;
            response = "Let's find products perfect for your skin! What's your skin type? (oily, dry, combination, sensitive)";
        }
        else if (intent.Contains("track") || intent.Contains("order") || intent.Contains("3"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
            response = "Please provide your order number to track your order.";
        }
        else if (intent.Contains("help") || intent.Contains("4"))
        {
            ctx.SetData("previousState", ConversationState.MainMenu);
            ctx.CurrentState = ConversationState.Help;
            response = "I can help you with:\n- Browsing products\n- Skin consultation\n- Order tracking\n\nWhat would you like to know?";
        }
        else
        {
            response = "Please choose an option:\n\n1. Browse products\n2. Skin consultation\n3. Track order\n4. Help";
        }

        AddToHistory(ctx, "model", response);
        return response;
    }
}
