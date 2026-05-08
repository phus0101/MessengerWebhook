using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Logging;

namespace MessengerWebhook.StateMachine.Handlers;

public class ProductDetailStateHandler : BaseStateHandler
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public override ConversationState HandledState => ConversationState.ProductDetail;

    public ProductDetailStateHandler(
        IGeminiService geminiService,
        IProductRepository productRepository,
        ITenantContext tenantContext,
        ILogger<ProductDetailStateHandler> logger)
        : base(geminiService, logger)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
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

        if (!_tenantContext.TenantId.HasValue)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Em chưa xác định được catalog của shop. Chị thử tìm lại sản phẩm giúp em nhé.";
        }

        var product = await _productRepository.GetActiveByIdAsync(productId, _tenantContext.TenantId.Value);
        if (product == null)
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Không tìm thấy sản phẩm trong catalog hiện tại. Hãy tìm lại nhé.";
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
            return "Để tôi kiểm tra xem sản phẩm này có phù hợp với da bạn không. Loại da của bạn là gì?";
        }

        if (intent.Contains("back") || intent.Contains("browse"))
        {
            ctx.CurrentState = ConversationState.BrowsingProducts;
            return "Quay lại tìm kiếm sản phẩm.";
        }

        // Default: show variants
        ctx.CurrentState = ConversationState.VariantSelection;
        var variants = product.Variants.Where(v => v.StockQuantity > 0).ToList();

        if (variants.Count == 0)
        {
            return "Xin lỗi, sản phẩm này đã hết hàng. Để tôi gợi ý sản phẩm tương tự.";
        }

        var variantList = string.Join("\n", variants.Select((v, i) =>
            $"{i + 1}. {v.VolumeML}ml {v.Texture} - {v.Price:N0}đ (còn {v.StockQuantity} sản phẩm)"));

        var response = $"{product.Name}\nCác tùy chọn có sẵn:\n\n{variantList}\n\nTrả lời số để thêm vào giỏ hàng.";
        AddToHistory(ctx, "model", response);
        return response;
    }
}
