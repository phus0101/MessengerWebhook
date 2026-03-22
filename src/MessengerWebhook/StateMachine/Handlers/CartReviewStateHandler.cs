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
        IStateMachine stateMachine,
        IProductRepository productRepository,
        ILogger<CartReviewStateHandler> logger)
        : base(geminiService, stateMachine, logger)
    {
        _productRepository = productRepository;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var cartItems = ctx.GetData<List<string>>("cartItems");
        if (cartItems == null || cartItems.Count == 0)
        {
            await TransitionToAsync(ctx, ConversationState.BrowsingProducts);
            return "Your cart is empty. Let's find some products!";
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
            await TransitionToAsync(ctx, ConversationState.ShippingAddress);
            return "Great! Let's proceed to checkout. Please provide your shipping address.";
        }

        if (intent.Contains("continue") || intent.Contains("shop") || intent.Contains("2"))
        {
            await TransitionToAsync(ctx, ConversationState.BrowsingProducts);
            return "Sure! What else are you looking for?";
        }

        // Show cart summary
        var total = cartItems.Count * 29.99m; // Simplified calculation
        var response = $"Your cart ({cartItems.Count} items):\nEstimated total: ${total:F2}\n\nOptions:\n1. Checkout\n2. Continue shopping";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
