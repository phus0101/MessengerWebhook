using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class GreetingStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Greeting;

    public GreetingStateHandler(
        IGeminiService geminiService,
        ILogger<GreetingStateHandler> logger)
        : base(geminiService, logger)
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

        var response = "Xin chào! Tôi ở đây để giúp bạn tìm sản phẩm mỹ phẩm hoàn hảo. ";

        if (intent.Contains("skin") || intent.Contains("analysis"))
        {
            ctx.CurrentState = ConversationState.SkinConsultation;
            response += "Hãy bắt đầu với tư vấn da nhé!";
        }
        else if (intent.Contains("track") || intent.Contains("order"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
            response += "Tôi có thể giúp bạn theo dõi đơn hàng.";
        }
        else
        {
            ctx.CurrentState = ConversationState.MainMenu;
            response += "Bạn muốn làm gì?\n\n1. Xem sản phẩm\n2. Tư vấn da\n3. Theo dõi đơn hàng\n4. Trợ giúp";
        }

        AddToHistory(ctx, "model", response);
        return response;
    }
}
