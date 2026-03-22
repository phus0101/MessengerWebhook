using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class AddToCartStateHandler : BaseStateHandler
{
    public override ConversationState HandledState => ConversationState.AddToCart;

    public AddToCartStateHandler(
        IGeminiService geminiService,
        IStateMachine stateMachine,
        ILogger<AddToCartStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        var variantId = ctx.GetData<string>("selectedVariantId");
        if (string.IsNullOrEmpty(variantId))
        {
            await TransitionToAsync(ctx, ConversationState.BrowsingProducts);
            return "Please select a product variant first.";
        }

        var cartItems = ctx.GetData<List<string>>("cartItems") ?? new List<string>();
        cartItems.Add(variantId);
        ctx.SetData("cartItems", cartItems);

        Logger.LogInformation("Added variant {VariantId} to cart for PSID: {PSID}", variantId, ctx.FacebookPSID);

        await TransitionToAsync(ctx, ConversationState.CartReview);

        var response = $"Added to cart! You have {cartItems.Count} item(s).\n\nWould you like to:\n1. View cart\n2. Continue shopping";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
