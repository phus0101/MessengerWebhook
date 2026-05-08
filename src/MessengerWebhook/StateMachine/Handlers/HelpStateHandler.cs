using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class HelpStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.Help;

    public HelpStateHandler(
        IGeminiService geminiService,
        
        ILogger<HelpStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var helpResponse = """
Dạ em có thể hỗ trợ chị:
1. Xem sản phẩm trong catalog
2. Tư vấn sản phẩm theo nhu cầu chăm sóc da
3. Ghi nhận thông tin đặt hàng khi chị đã chọn sản phẩm
4. Chuyển nhân viên hỗ trợ nếu cần kiểm tra chính sách hoặc đơn hàng
""";

        // Return to previous state or main menu
        var previousState = ctx.GetData<ConversationState?>("previousState");
        if (previousState.HasValue && previousState.Value != ConversationState.Help)
        {
            ctx.CurrentState = previousState.Value;
            helpResponse += "\n\nQuay lại nơi bạn đang ở...";
        }
        else
        {
            ctx.CurrentState = ConversationState.MainMenu;
            helpResponse += "\n\nQuay lại menu chính.";
        }

        AddToHistory(ctx, "model", helpResponse);
        return helpResponse;
    }
}
