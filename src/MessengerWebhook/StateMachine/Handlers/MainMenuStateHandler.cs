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
            response = "Tuyệt vời! Bạn đang tìm loại sản phẩm nào? (ví dụ: kem dưỡng ẩm, serum, sữa rửa mặt)";
        }
        else if (intent.Contains("skin") || intent.Contains("consultation") || intent.Contains("2"))
        {
            ctx.CurrentState = ConversationState.SkinConsultation;
            response = "Hãy tìm sản phẩm hoàn hảo cho làn da của bạn! Loại da của bạn là gì? (da dầu, da khô, da hỗn hợp, da nhạy cảm)";
        }
        else if (intent.Contains("track") || intent.Contains("order") || intent.Contains("3"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
            response = "Vui lòng cung cấp mã đơn hàng để theo dõi.";
        }
        else if (intent.Contains("help") || intent.Contains("4"))
        {
            ctx.SetData("previousState", ConversationState.MainMenu);
            ctx.CurrentState = ConversationState.Help;
            response = "Tôi có thể giúp bạn:\n- Xem sản phẩm\n- Tư vấn da\n- Theo dõi đơn hàng\n\nBạn muốn biết gì?";
        }
        else
        {
            response = "Vui lòng chọn một tùy chọn:\n\n1. Xem sản phẩm\n2. Tư vấn da\n3. Theo dõi đơn hàng\n4. Trợ giúp";
        }

        AddToHistory(ctx, "model", response);
        return response;
    }
}
