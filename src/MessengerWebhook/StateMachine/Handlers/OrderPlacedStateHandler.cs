using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderPlacedStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderPlaced;

    public OrderPlacedStateHandler(
        IGeminiService geminiService,
        ILogger<OrderPlacedStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for track order command
        if (lowerMessage.Contains("track") || lowerMessage.Contains("theo dõi") || lowerMessage.Contains("kiểm tra"))
        {
            ctx.CurrentState = ConversationState.OrderTracking;
            var orderId = ctx.GetData<string>("orderId") ?? "N/A";

            var reply = $@"Theo dõi đơn hàng - {orderId}

Trạng thái: Đang xử lý
- Đơn hàng đã xác nhận ✓
- Đã nhận thanh toán ✓
- Đang chuẩn bị hàng...

Chúng tôi sẽ thông báo khi đơn hàng được giao!";

            AddToHistory(ctx, "model", reply);
            return reply;
        }

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

        // Default response
        var defaultReply = "Đơn hàng của bạn đã được đặt! Gõ 'theo dõi đơn hàng' để kiểm tra trạng thái hoặc 'menu' để về menu chính.";
        AddToHistory(ctx, "model", defaultReply);
        return defaultReply;
    }
}
