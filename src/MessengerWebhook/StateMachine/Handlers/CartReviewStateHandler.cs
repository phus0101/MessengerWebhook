using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class CartReviewStateHandler : BaseStateHandler
{
    private readonly IProductRepository _productRepository;

    public override ConversationState HandledState => ConversationState.CartReview;

    public CartReviewStateHandler(
        IGeminiService geminiService,
        
        IProductRepository productRepository,
        ILogger<CartReviewStateHandler> logger)
        : base(geminiService, logger)
    {
        _productRepository = productRepository;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var cartItems = ctx.GetData<List<string>>("cartItems");
        if (cartItems == null || cartItems.Count == 0)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Giỏ hàng của bạn đang trống. Hãy tìm sản phẩm nhé!";
        }

        // Check user intent
        var prompt = $@"User said: '{message}'
Detect intent: checkout, continue_shopping, or view_cart.
Respond with ONLY the intent name.";

        var history = GetHistory(ctx);
        var intent = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        intent = intent.Trim().ToLowerInvariant();

        if (intent.Contains("checkout") || intent.Contains("1"))
        {
            ctx.CurrentState = ConversationState.ShippingAddress;
            return "Tuyệt vời! Tiến hành thanh toán. Vui lòng cung cấp địa chỉ giao hàng.";
        }

        if (intent.Contains("continue") || intent.Contains("shop") || intent.Contains("2"))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Được rồi! Bạn đang tìm gì nữa?";
        }

        // Show cart summary
        var total = cartItems.Count * 29.99m; // Simplified calculation
        var response = $"Giỏ hàng của bạn ({cartItems.Count} sản phẩm):\nTổng tạm tính: {total:N0}đ\n\nTùy chọn:\n1. Thanh toán\n2. Tiếp tục mua sắm";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
