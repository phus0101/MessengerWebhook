using System.Globalization;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.Sales.Reply;

/// <summary>
/// Domain-specific consultation reply builders extracted from SalesStateHandlerBase (R-05).
/// Handles offer, product, shipping, price, inventory, greeting, ambiguity, and order-confirmation replies.
/// </summary>
public class SalesConsultationReplies : ISalesConsultationReplies
{
    private readonly ISalesContextResolver _contextResolver;
    private readonly ISalesPromptBuilder _promptBuilder;
    private readonly IProductMappingService _productMappingService;
    private readonly ILogger<SalesConsultationReplies> _logger;

    public SalesConsultationReplies(
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder,
        IProductMappingService productMappingService,
        ILogger<SalesConsultationReplies> logger)
    {
        _contextResolver = contextResolver;
        _promptBuilder = promptBuilder;
        _productMappingService = productMappingService;
        _logger = logger;
    }

    public async Task<string?> TryBuildOfferResponseAsync(
        StateContext ctx,
        string message,
        CustomerIntent intent)
    {
        _logger.LogInformation(
            "TryBuildOfferResponseAsync called for PSID: {PSID}, Intent: {Intent}, MessageLength={MessageLength}",
            ctx.FacebookPSID, intent, message.Length);

        var product = await _contextResolver.ResolveCurrentProductAsync(ctx, message);
        if (product == null)
        {
            _logger.LogWarning("No product found for PSID: {PSID}, returning null", ctx.FacebookPSID);
            return null;
        }

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

        var productCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (productCodes.Count == 0)
        {
            productCodes = new List<string> { product.Code };
            ctx.SetData("selectedProductCodes", productCodes);
        }

        var giftCode = ctx.GetData<string>("selectedGiftCode");
        var giftName = ctx.GetData<string>("selectedGiftName");
        Gift? gift = null;
        if (!string.IsNullOrWhiteSpace(giftCode) || !string.IsNullOrWhiteSpace(giftName))
            gift = new Gift { Code = giftCode ?? string.Empty, Name = giftName ?? string.Empty };

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
        var priceMessage = snapshot?.PriceConfirmed == true && snapshot.ConfirmedPrice.HasValue
            ? $"Dạ bên em có {product.Name}, giá {snapshot.ConfirmedPrice.Value:N0}đ ạ."
            : $"Dạ bên em có {product.Name} ạ. Giá chính xác em cần chốt theo phiên bản hiện tại rồi báo chị chuẩn hơn nha.";
        var readyToBuyPriceMessage = snapshot?.PriceConfirmed == true && snapshot.ConfirmedPrice.HasValue
            ? $"Dạ em lên thông tin cho {product.Name} rồi nha, giá {snapshot.ConfirmedPrice.Value:N0}đ ạ."
            : $"Dạ em lên thông tin cho {product.Name} rồi nha. Giá chính xác em cần chốt theo phiên bản hiện tại rồi báo chị chuẩn hơn nha.";
        var shippingMessage = "Về phí ship, em cần kiểm tra lại theo địa chỉ cụ thể của chị để báo chính xác nha.";
        var giftMessage = gift == null
            ? "Hiện tại em chưa thấy quà tặng nào được xác nhận cho sản phẩm này ạ."
            : $"Quà tặng kèm theo là {gift.Name} ạ. Nếu có ưu đãi khác, em sẽ cập nhật thêm khi chốt đơn.";

        if (intent == CustomerIntent.Browsing)
        {
            return string.Join(Environment.NewLine, new[]
            {
                priceMessage, string.Empty, shippingMessage, string.Empty, giftMessage, string.Empty,
                "Chị muốn em tư vấn thêm về công dụng hay cách dùng không ạ?"
            });
        }

        return string.Join(Environment.NewLine, new[]
        {
            readyToBuyPriceMessage, string.Empty, shippingMessage, string.Empty, giftMessage, string.Empty,
            SalesMessageParser.BuildMissingInfoPrompt(ctx)
        });
    }

    public async Task<string?> BuildProductConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
            return null;

        ctx.SetData("inventory_confirmed", false);

        var lines = new List<string>
        {
            $"Dạ {product.Name} bên em là {_promptBuilder.NormalizeSentence(product.Description)}"
        };

        if (product.Code.Equals("KCN", StringComparison.OrdinalIgnoreCase))
            lines.Add("Sản phẩm này hợp khi chị hay đi ngoài trời vì ưu tiên bảo vệ da trước nắng và tia UV.");
        else if (product.Code.Equals("KL", StringComparison.OrdinalIgnoreCase))
            lines.Add("Dòng này thiên về cấp ẩm và giữ da mềm hơn, hợp khi da dễ khô hoặc thiếu ẩm do nắng gió.");
        else if (product.Code.Equals("MN", StringComparison.OrdinalIgnoreCase))
            lines.Add("Dòng này thiên về dưỡng ẩm và phục hồi da qua đêm, hợp khi da đang khô hoặc thiếu ẩm.");

        lines.Add($"Nếu chị muốn em nói kỹ hơn về công dụng chính hoặc cách dùng của {product.Name} thì em tư vấn tiếp ạ.");
        return string.Join(Environment.NewLine, lines);
    }

    public async Task<string?> BuildShippingConsultationReplyAsync(StateContext ctx, string message)
    {
        var lockedProductCode = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).FirstOrDefault();
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        var effectiveProductCode = product?.Code ?? lockedProductCode;
        if (string.IsNullOrWhiteSpace(effectiveProductCode) || product == null)
            return null;

        ctx.SetData("selectedProductCodes", new List<string> { effectiveProductCode });
        await _contextResolver.SyncActiveProductPolicyContextAsync(ctx, effectiveProductCode);

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
        ctx.SetData("shipping_policy_confirmed", false);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("inventory_confirmed", snapshot?.InventoryConfirmed == true);

        var shippingMessage = "em chưa dám chốt freeship hay phí ship ngay lúc này, để em kiểm tra lại theo đơn cụ thể rồi báo chị chính xác ạ";
        var giftMessage = snapshot?.GiftConfirmed == true
            ? $"Theo dữ liệu nội bộ hiện tại thì quà tặng đang gắn với sản phẩm này là {snapshot.GiftName} ạ."
            : "Hiện tại em chưa thấy quà tặng nào được xác nhận rõ cho sản phẩm này ạ.";

        return $"Dạ với {product.Name}, {shippingMessage} {giftMessage} Nếu chị cần em tính lại theo đơn cụ thể hoặc hỗ trợ chốt đơn thì em làm tiếp cho mình nha.";
    }

    public async Task<string?> BuildOrderEstimateReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
            return null;

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

        var quantities = ctx.GetData<Dictionary<string, int>>("selectedProductQuantities")
                         ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var quantity = quantities.TryGetValue(product.Code, out var selectedQuantity) && selectedQuantity > 0
            ? selectedQuantity
            : 1;

        var giftName = ctx.GetData<string>("selectedGiftName");
        var merchandiseTotal = product.BasePrice * quantity;
        var totalProducts = quantity + (string.IsNullOrWhiteSpace(giftName) ? 0 : 1);
        var unitLabel = quantity > 1 ? $"{quantity} hũ {product.Name}" : $"1 hũ {product.Name}";
        var giftLabel = string.IsNullOrWhiteSpace(giftName) ? string.Empty : $" + 1 quà tặng {giftName}";

        return $"Dạ nếu mình chốt {product.Name} thì đơn đang có tổng cộng {totalProducts} sản phẩm gồm {unitLabel}{giftLabel} ạ. Tạm tính tiền hàng hiện tại là {merchandiseTotal:N0}đ, còn phí ship và tổng đơn cuối em cần kiểm tra lại theo đơn cụ thể rồi báo chị chính xác nha.";
    }

    public async Task<string> BuildFirstGreetingReplyAsync(StateContext ctx)
    {
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var customerName = ctx.GetData<string>("customerName") ?? ctx.GetData<string>("rememberedCustomerName");
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);

        if (vipProfile?.IsVip == true)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                ? $"Dạ em chào chị {customerName} ạ, lâu rồi mới thấy chị ghé lại. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?"
                : "Dạ em chào chị ạ, lâu rồi mới thấy chị ghé lại. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?";
        }

        if (isReturningCustomer)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                ? $"Dạ em chào chị {customerName} ạ, em rất vui được hỗ trợ chị lại nè. Hôm nay chị đang cần em tư vấn sản phẩm nào ạ?"
                : "Dạ em chào chị ạ, em rất vui được hỗ trợ chị lại nè. Hôm nay chị đang cần em tư vấn sản phẩm nào ạ?";
        }

        return "Dạ em chào chị ạ. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?";
    }

    public async Task<string?> BuildPriceConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
            return null;

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotAsync(ctx, product);
        if (snapshot == null)
            return null;

        ctx.SetData("price_confirmed", snapshot.PriceConfirmed);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("inventory_confirmed", snapshot.InventoryConfirmed);

        var productLabel = snapshot.PriceLabel == null ? product.Name : $"{product.Name} bản {snapshot.PriceLabel}";
        var lines = new List<string>();

        if (snapshot.PriceConfirmed && snapshot.ConfirmedPrice.HasValue)
            lines.Add($"Dạ {productLabel} hiện bên em đang để giá {snapshot.ConfirmedPrice.Value:N0}đ theo dữ liệu nội bộ ạ.");
        else
            lines.Add($"Dạ với {product.Name}, em chưa dám chốt giá chính xác ngay lúc này ạ. Em cần kiểm tra lại đúng phiên bản và dữ liệu runtime hiện tại rồi báo chị chuẩn hơn nha.");

        if (snapshot.GiftConfirmed)
            lines.Add($"Quà tặng đang gắn theo dữ liệu nội bộ hiện tại là {snapshot.GiftName}, còn ưu đãi khác thì em cần kiểm tra lại ở lúc chốt đơn để báo chị chính xác nha.");
        else
            lines.Add("Ưu đãi hiện tại em sẽ kiểm tra lại theo chính sách áp dụng ở lúc chốt đơn để báo chị chính xác nha.");

        lines.Add("Nếu chị muốn em tính luôn tổng tiền tạm tính hoặc hỗ trợ chốt đơn thì chị nhắn em nha.");
        return string.Join(" ", lines);
    }

    public async Task<string?> BuildInventoryConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
            return null;

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotAsync(ctx, product);
        if (snapshot == null)
            return null;

        ctx.SetData("inventory_confirmed", snapshot.InventoryConfirmed);

        if (!snapshot.InventoryConfirmed)
            return $"Dạ với {product.Name}, em chưa xác nhận tồn kho chắc ngay lúc này ạ. Để em kiểm tra lại theo phiên bản cụ thể rồi báo chị chính xác nha.";

        if (snapshot.IsInStock == true)
            return $"Dạ với {product.Name} bản {snapshot.PriceLabel}, theo dữ liệu nội bộ hiện tại thì còn khoảng {snapshot.StockQuantity} sản phẩm ạ.";

        return $"Dạ với {product.Name} bản {snapshot.PriceLabel}, theo dữ liệu nội bộ hiện tại thì em đang chưa thấy còn hàng ạ. Nếu chị muốn em kiểm tra phương án khác thì em hỗ trợ tiếp nha.";
    }

    public async Task<string?> BuildAmbiguousProductClarificationReplyAsync(StateContext ctx)
    {
        var recentMessages = ConversationHistoryHelper.GetHistory(ctx).TakeLast(10).ToList();
        var userCandidates = await _contextResolver.CollectHistoryProductCandidatesAsync(recentMessages, "user");
        var assistantCandidates = await _contextResolver.CollectHistoryProductCandidatesAsync(recentMessages, "assistant");
        var candidates = userCandidates
            .Concat(assistantCandidates)
            .GroupBy(x => x.Product.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(3)
            .ToList();

        if (candidates.Count <= 1)
            return null;

        var labels = candidates.Select(x => x.Product.Name).ToList();
        var joinedLabels = labels.Count == 2
            ? $"{labels[0]} hay {labels[1]}"
            : string.Join(", ", labels.Take(labels.Count - 1)) + $" hay {labels.Last()}";

        return $"Dạ để em chốt đúng ý chị thì chị giúp em xác nhận mình đang nói tới {joinedLabels} ạ?";
    }

    public async Task<string?> BuildFinalOrderConfirmationReplyAsync(
        StateContext ctx,
        string message,
        bool forceResend = false)
    {
        if (!forceResend && SalesMessageParser.IsAwaitingFinalSummaryConfirmation(ctx))
            return null;

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count == 0)
            return null;

        var products = new List<Product>();
        foreach (var productCode in selectedCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var product = await _productMappingService.GetActiveProductByCodeAsync(productCode);
            if (product != null)
                products.Add(product);
        }

        if (products.Count == 0)
            return null;

        var quantities = ctx.GetData<Dictionary<string, int>>("selectedProductQuantities")
                         ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var itemLabels = new List<string>();
        decimal merchandiseTotal = 0;

        foreach (var product in products)
        {
            var quantity = quantities.TryGetValue(product.Code, out var selectedQuantity) && selectedQuantity > 0
                ? selectedQuantity
                : 1;
            merchandiseTotal += product.BasePrice * quantity;
            itemLabels.Add(quantity > 1 ? $"{product.Name} x{quantity}" : $"{product.Name} x1");
        }

        var giftName = ctx.GetData<string>("selectedGiftName");
        var phone = ctx.GetData<string>("customerPhone");
        var address = ctx.GetData<string>("shippingAddress");

        ctx.SetData("awaitingFinalSummaryConfirmation", true);
        ctx.SetData("finalSummaryShownAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        ctx.SetData("final_price_summary_ready", true);
        ctx.SetData("price_confirmed", true);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("shipping_policy_confirmed", false);
        ctx.SetData("inventory_confirmed", false);

        var lines = new List<string>
        {
            "Dạ em tóm tắt đơn của chị như này ạ:",
            $"- Sản phẩm: {string.Join(", ", itemLabels)}",
            $"- Tiền hàng tạm tính: {merchandiseTotal:N0}đ",
            "- Phí ship: em cần kiểm tra lại theo đơn cụ thể trước khi chốt",
            "- Tổng đơn cuối: em sẽ báo lại sau khi kiểm tra đủ phí ship và chính sách áp dụng",
            $"- SĐT nhận hàng: {phone}",
            $"- Địa chỉ giao hàng: {address}"
        };

        if (!string.IsNullOrWhiteSpace(giftName))
            lines.Insert(4, $"- Quà tặng theo dữ liệu nội bộ hiện tại: {giftName}");

        lines.Add("Nếu chị đồng ý đơn này thì chị nhắn em kiểu như \"đúng rồi\" hoặc \"chốt đơn giúp chị\" để em lên đơn nháp nha.");
        return string.Join(Environment.NewLine, lines);
    }
}
