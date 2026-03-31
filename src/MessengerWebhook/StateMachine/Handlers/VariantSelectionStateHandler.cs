using MessengerWebhook.Models;
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
            return "Vui lòng chọn sản phẩm trước.";
        }

        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Không tìm thấy sản phẩm.";
        }

        // Check for back command
        if (message.ToLowerInvariant().Contains("back") || message.ToLowerInvariant().Contains("quay"))
        {
            ctx.CurrentState = ConversationState.ProductDetail;
            return "Quay lại chi tiết sản phẩm.";
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
                var response = $"Đã chọn: {product.Name} - {selectedVariant.VolumeML}ml {selectedVariant.Texture} ({selectedVariant.Price:N0}đ). Đang thêm vào giỏ hàng...";
                AddToHistory(ctx, "model", response);
                return response;
            }
        }

        var reply = "Vui lòng trả lời số hợp lệ để chọn phiên bản, hoặc gõ 'quay lại' để trở về.";
        AddToHistory(ctx, "model", reply);
        return reply;
    }
}
