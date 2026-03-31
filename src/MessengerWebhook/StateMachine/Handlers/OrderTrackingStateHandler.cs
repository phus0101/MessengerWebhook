using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderTrackingStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderTracking;

    public OrderTrackingStateHandler(
        IGeminiService geminiService,
        ILogger<OrderTrackingStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for menu command
        if (lowerMessage.Contains("menu") || lowerMessage.Contains("main") || lowerMessage.Contains("chính"))
        {
            ctx.CurrentState = ConversationState.MainMenu;
            var response = @"Menu chính:
1. Xem sản phẩm
2. Phân tích da
3. Theo dõi đơn hàng
4. Trợ giúp

Bạn muốn làm gì?";

            AddToHistory(ctx, "model", response);
            return response;
        }

        // Show tracking details
        var orderId = ctx.GetData<string>("orderId") ?? "N/A";

        var reply = $@"Theo dõi đơn hàng - {orderId}

Trạng thái hiện tại: Đang xử lý
- Đơn hàng đã xác nhận ✓
- Đã nhận thanh toán ✓
- Đang chuẩn bị hàng...

Dự kiến giao hàng: 3-5 ngày làm việc

Gõ 'menu' để quay lại menu chính.";

        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
