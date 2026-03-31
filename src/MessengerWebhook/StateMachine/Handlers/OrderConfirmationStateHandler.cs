using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class OrderConfirmationStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.OrderConfirmation;

    public OrderConfirmationStateHandler(
        IGeminiService geminiService,
        ILogger<OrderConfirmationStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var lowerMessage = message.ToLowerInvariant();

        // Check for back command
        if (lowerMessage.Contains("back") || lowerMessage.Contains("modify") || lowerMessage.Contains("quay") || lowerMessage.Contains("sửa"))
        {
            ctx.CurrentState = ConversationState.PaymentMethod;
            return "Quay lại chọn phương thức thanh toán.";
        }

        // Check for confirmation
        if (lowerMessage.Contains("confirm") || lowerMessage.Contains("yes") || lowerMessage.Contains("place") || lowerMessage.Contains("xác nhận") || lowerMessage.Contains("đặt"))
        {
            // Generate order ID
            var orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            ctx.SetData("orderId", orderId);

            Logger.LogInformation("Order placed: {OrderId} for PSID: {PSID}", orderId, ctx.FacebookPSID);

            ctx.CurrentState = ConversationState.OrderPlaced;

            var reply = $@"✅ Đặt hàng thành công!

Mã đơn hàng: {orderId}
Dự kiến giao hàng: 3-5 ngày làm việc

Bạn có thể theo dõi đơn hàng bất cứ lúc nào bằng cách gõ 'theo dõi đơn hàng'.
Gõ 'menu' để quay lại menu chính.";

            AddToHistory(ctx, "model", reply);
            return reply;
        }

        var response = "Vui lòng gõ 'xác nhận' để đặt hàng hoặc 'quay lại' để sửa đổi.";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
