using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class ShippingAddressStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.ShippingAddress;

    public ShippingAddressStateHandler(
        IGeminiService geminiService,
        
        ILogger<ShippingAddressStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        // Check for back command
        if (message.ToLowerInvariant().Contains("back") || message.ToLowerInvariant().Contains("cart") || message.ToLowerInvariant().Contains("quay"))
        {
            ctx.CurrentState = ConversationState.CartReview;
            return "Quay lại xem giỏ hàng.";
        }

        // Use Gemini to validate and parse address
        var prompt = $@"User provided address: '{message}'
Validate if this looks like a valid shipping address (street, city, postal code).
Respond with 'valid' or 'invalid'.";

        var history = GetHistory(ctx);
        var validation = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        validation = validation.Trim().ToLowerInvariant();

        if (validation.Contains("invalid"))
        {
            var response = "Vui lòng cung cấp địa chỉ đầy đủ bao gồm đường, thành phố và mã bưu điện.";
            AddToHistory(ctx, "model", response);
            return response;
        }

        ctx.SetData("shippingAddress", message);
        Logger.LogInformation("Shipping address saved for PSID: {PSID}", ctx.FacebookPSID);

        ctx.CurrentState = ConversationState.PaymentMethod;

        var reply = "Đã lưu địa chỉ! Bây giờ, vui lòng chọn phương thức thanh toán:\n1. Thẻ tín dụng\n2. PayPal\n3. Thanh toán khi nhận hàng";
        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
