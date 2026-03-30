using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.ProductMapping;

namespace MessengerWebhook.Services.QuickReply;

/// <summary>
/// Handler for Quick Reply and Postback events from Facebook Messenger
/// </summary>
public class QuickReplyHandler : IQuickReplyHandler
{
    private readonly IProductMappingService _productMappingService;
    private readonly IGiftSelectionService _giftSelectionService;
    private readonly IFreeshipCalculator _freeshipCalculator;

    public QuickReplyHandler(
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator)
    {
        _productMappingService = productMappingService;
        _giftSelectionService = giftSelectionService;
        _freeshipCalculator = freeshipCalculator;
    }

    public async Task<string> HandleQuickReplyAsync(string senderId, string payload)
    {
        return await ProcessPayloadAsync(payload);
    }

    public async Task<string> HandlePostbackAsync(string senderId, string payload)
    {
        return await ProcessPayloadAsync(payload);
    }

    private async Task<string> ProcessPayloadAsync(string payload)
    {
        // Get product from payload
        var product = await _productMappingService.GetProductByPayloadAsync(payload);
        if (product == null)
        {
            return "Xin lỗi, em không tìm thấy sản phẩm này. Chị vui lòng thử lại nhé! 🙏";
        }

        // Get gift for product
        var gift = await _giftSelectionService.SelectGiftForProductAsync(product.Code);

        // Calculate freeship
        var productCodes = new List<string> { product.Code };
        var isEligibleForFreeship = _freeshipCalculator.IsEligibleForFreeship(productCodes);
        var freeshipMessage = _freeshipCalculator.GetFreeshipMessage(isEligibleForFreeship);

        // Format response message
        return FormatResponseMessage(product, gift, freeshipMessage);
    }

    private string FormatResponseMessage(Product product, Gift? gift, string freeshipMessage)
    {
        var message = $"✨ Dạ em xin phép gửi chị thông tin:\n\n";
        message += $"📦 Sản phẩm: {product.Name} ({product.BasePrice:N0}đ)\n";

        if (gift != null)
        {
            message += $"🎁 Quà tặng: {gift.Name}\n";
        }

        message += $"\nTổng cộng: {product.BasePrice:N0}đ {freeshipMessage}\n\n";
        message += "Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕";

        return message;
    }
}
