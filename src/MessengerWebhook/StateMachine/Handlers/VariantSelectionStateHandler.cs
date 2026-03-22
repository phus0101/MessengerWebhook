using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class VariantSelectionStateHandler : BaseStateHandler
{
    private readonly IProductRepository _productRepository;

    public override ConversationState HandledState => ConversationState.VariantSelection;

    public VariantSelectionStateHandler(
        IGeminiService geminiService,
        
        IProductRepository productRepository,
        ILogger<VariantSelectionStateHandler> logger)
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
            return "Product not found.";
        }

        // Check for back command
        if (message.ToLowerInvariant().Contains("back"))
        {
            ctx.CurrentState = ConversationState.ProductDetail;
            return "Returning to product details.";
        }

        // Parse variant selection
        if (int.TryParse(message.Trim(), out int selection))
        {
            var variants = product.Variants.Where(v => v.StockQuantity > 0).ToList();
            if (selection > 0 && selection <= variants.Count)
            {
                var selectedVariant = variants[selection - 1];
                ctx.SetData("selectedVariantId", selectedVariant.Id);

                ctx.CurrentState = ConversationState.AddToCart;
                var response = $"Selected: {product.Name} - {selectedVariant.VolumeML}ml {selectedVariant.Texture} (${selectedVariant.Price:F2}). Adding to cart...";
                AddToHistory(ctx, "model", response);
                return response;
            }
        }

        var reply = "Please reply with a valid number to select a variant, or type 'back' to return.";
        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
