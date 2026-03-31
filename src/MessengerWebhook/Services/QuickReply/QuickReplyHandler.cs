using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.QuickReply;

/// <summary>
/// Handler for Quick Reply and Postback events from Facebook Messenger.
/// </summary>
public class QuickReplyHandler : IQuickReplyHandler
{
    private readonly IProductMappingService _productMappingService;
    private readonly IGiftSelectionService _giftSelectionService;
    private readonly IFreeshipCalculator _freeshipCalculator;
    private readonly IStateMachine? _stateMachine;
    private readonly ILogger<QuickReplyHandler>? _logger;

    public QuickReplyHandler(
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator)
        : this(productMappingService, giftSelectionService, freeshipCalculator, null, null)
    {
    }

    public QuickReplyHandler(
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        IStateMachine? stateMachine,
        ILogger<QuickReplyHandler>? logger)
    {
        _productMappingService = productMappingService;
        _giftSelectionService = giftSelectionService;
        _freeshipCalculator = freeshipCalculator;
        _stateMachine = stateMachine;
        _logger = logger;
    }

    public Task<string> HandleQuickReplyAsync(string senderId, string payload)
    {
        return ProcessPayloadAsync(senderId, payload, null);
    }

    public Task<string> HandleQuickReplyAsync(string senderId, string payload, string? pageId)
    {
        return ProcessPayloadAsync(senderId, payload, pageId);
    }

    public Task<string> HandlePostbackAsync(string senderId, string payload)
    {
        return ProcessPayloadAsync(senderId, payload, null);
    }

    public Task<string> HandlePostbackAsync(string senderId, string payload, string? pageId)
    {
        return ProcessPayloadAsync(senderId, payload, pageId);
    }

    private async Task<string> ProcessPayloadAsync(string senderId, string payload, string? pageId)
    {
        var product = await _productMappingService.GetProductByPayloadAsync(payload);
        if (product == null)
        {
            return "Dạ em chưa thấy mã sản phẩm này trong hệ thống. Chị nhắn lại giúp em để em hỗ trợ lên đơn ngay nha.";
        }

        var gift = await _giftSelectionService.SelectGiftForProductAsync(product.Code);
        var productCodes = new List<string> { product.Code };
        var shippingFee = _freeshipCalculator.CalculateShippingFee(productCodes);
        var freeshipMessage = _freeshipCalculator.GetFreeshipMessage(shippingFee == 0);

        var context = await PersistSalesContextAsync(senderId, pageId, product, gift, shippingFee);
        var callToAction = context == null
            ? "Chi iu cho em xin so dien thoai va dia chi em len don luon nha."
            : SalesMessageParser.BuildMissingInfoPrompt(context);

        return FormatResponseMessage(product, gift, freeshipMessage, callToAction);
    }

    private async Task<StateContext?> PersistSalesContextAsync(
        string senderId,
        string? pageId,
        Product product,
        Gift? gift,
        decimal shippingFee)
    {
        if (_stateMachine == null)
        {
            return null;
        }

        try
        {
            var context = await _stateMachine.LoadOrCreateAsync(senderId, pageId);
            context.SetData("facebookPageId", pageId ?? context.GetData<string>("facebookPageId") ?? string.Empty);
            context.SetData("selectedProductCodes", new List<string> { product.Code });
            context.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
            context.SetData("selectedGiftName", gift?.Name ?? string.Empty);
            context.SetData("shippingFee", shippingFee);
            context.SetData("quickReplyPayload", $"PRODUCT_{product.Code}");
            context.CurrentState = ConversationState.QuickReplySales;

            await _stateMachine.SaveAsync(context);
            return context;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Unable to persist quick reply context for {SenderId}", senderId);
            return null;
        }
    }

    private static string FormatResponseMessage(Product product, Gift? gift, string freeshipMessage, string callToAction)
    {
        var lines = new List<string>
        {
            "Dạ em chốt nhanh cho chị thông tin đang chọn nè:",
            string.Empty,
            $"San pham: {product.Name} ({product.BasePrice:N0}d)"
        };

        if (gift != null)
        {
            lines.Add($"Qua tang: {gift.Name}");
        }

        lines.Add($"Chinh sach ship: {freeshipMessage}");
        lines.Add(string.Empty);
        lines.Add(callToAction);

        return string.Join(Environment.NewLine, lines);
    }
}
