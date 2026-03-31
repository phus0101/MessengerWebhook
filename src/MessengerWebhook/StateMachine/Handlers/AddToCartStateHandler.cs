using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class AddToCartStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.AddToCart;

    public AddToCartStateHandler(
        IGeminiService geminiService,
        
        ILogger<AddToCartStateHandler> logger)
        : base(geminiService, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        var variantId = ctx.GetData<string>("selectedVariantId");
        if (string.IsNullOrEmpty(variantId))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Vui lòng chọn phiên bản sản phẩm trước.";
        }

        var cartItems = ctx.GetData<List<string>>("cartItems") ?? new List<string>();
        cartItems.Add(variantId);
        ctx.SetData("cartItems", cartItems);

        Logger.LogInformation("Added variant {VariantId} to cart for PSID: {PSID}", variantId, ctx.FacebookPSID);

        ctx.CurrentState = ConversationState.CartReview;

        var response = $"Đã thêm vào giỏ hàng! Bạn có {cartItems.Count} sản phẩm.\n\nBạn muốn:\n1. Xem giỏ hàng\n2. Tiếp tục mua sắm";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
