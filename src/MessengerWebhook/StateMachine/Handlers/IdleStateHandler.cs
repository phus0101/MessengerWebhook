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
        AddToHistory(ctx, "user", message);

        // Analyze intent from first message
        var intentPrompt = $@"User's first message: '{message}'

Analyze the intent and respond with ONE of these exact values:
- skin_consultation: if asking about skin type, skin concerns, product recommendations for their skin
- browse_products: if asking to see products, browse catalog, or general shopping
- order_tracking: if asking about order status or tracking
- help: if asking for help or support
- greeting: if just saying hello without specific request

Respond with ONLY the intent keyword.";

        var history = GetHistory(ctx);
        var intent = await GeminiService.SendMessageAsync(ctx.FacebookPSID, intentPrompt, history);
        intent = intent.Trim().ToLowerInvariant();

        Logger.LogInformation("First message intent: {Intent} for PSID: {PSID}", intent, ctx.FacebookPSID);

        // Generate natural response based on intent
        var responsePrompt = $@"User's first message: '{message}'
Intent detected: {intent}

Generate a natural, friendly greeting response in Vietnamese that:
1. Acknowledges what they asked about
2. Shows you understand their need
3. Offers to help with their specific request
4. Keep it conversational and warm (2-3 sentences max)

Respond naturally as a helpful beauty consultant.";

        var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, responsePrompt, history);

        // Transition to appropriate state
        if (intent.Contains("skin"))
        {
            ctx.CurrentState = ConversationState.SkinConsultation;
        }
        else if (intent.Contains("browse") || intent.Contains("product"))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
        }
        else if (intent.Contains("track") || intent.Contains("order"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
        }
        else
        {
            ctx.CurrentState = ConversationState.MainMenu;
            response += "\n\nBạn muốn:\n1. Xem sản phẩm\n2. Tư vấn da\n3. Theo dõi đơn hàng\n4. Trợ giúp";
        }

        AddToHistory(ctx, "model", response);
        return response;
    }
}
