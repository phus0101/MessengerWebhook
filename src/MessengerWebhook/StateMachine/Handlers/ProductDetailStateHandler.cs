using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class ProductDetailStateHandler : BaseStateHandler
{
    private readonly IProductRepository _productRepository;

    public override ConversationState HandledState => ConversationState.ProductDetail;

    public ProductDetailStateHandler(
        IGeminiService geminiService,
        
        IProductRepository productRepository,
        ILogger<ProductDetailStateHandler> logger)
        : base(geminiService, logger)
    {
        _productRepository = productRepository;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        var productId = ctx.GetData<string>("selectedProductId");
        if (string.IsNullOrEmpty(productId))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Please select a product first.";
        }

        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Product not found. Let's search again.";
        }

        // Check user intent
        var prompt = $@"User said: '{message}' while viewing product details.
Detect intent: skin_analysis, select_variant, back_to_browsing, or view_details.
Respond with ONLY the intent name.";

        var history = GetHistory(ctx);
        var intent = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);
        intent = intent.Trim().ToLowerInvariant();

        if (intent.Contains("skin") || intent.Contains("analysis"))
        {
            ctx.CurrentState = ConversationState.SkinAnalysis;
            return "Let me check if this product suits your skin. What's your skin type?";
        }

        if (intent.Contains("back") || intent.Contains("browse"))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Returning to product search.";
        }

        // Default: show variants
        ctx.CurrentState = ConversationState.VariantSelection;
        var variants = product.Variants.Where(v => v.StockQuantity > 0).ToList();

        if (variants.Count == 0)
        {
            return "Sorry, this product is out of stock. Let me show you similar products.";
        }

        var variantList = string.Join("\n", variants.Select((v, i) =>
            $"{i + 1}. {v.VolumeML}ml {v.Texture} - ${v.Price:F2} ({v.StockQuantity} in stock)"));

        var response = $"Available options:\n\n{variantList}\n\nReply with a number to add to cart.";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
