using MessengerWebhook.Models;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.Services.Survey;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.Services;

public class QuickReplyHandlerTests
{
    private readonly Mock<IProductMappingService> _productMappingService = new();
    private readonly Mock<IGiftSelectionService> _giftSelectionService = new();
    private readonly Mock<IFreeshipCalculator> _freeshipCalculator = new();
    private readonly QuickReplyHandler _handler;

    public QuickReplyHandlerTests()
    {
        _handler = new QuickReplyHandler(
            _productMappingService.Object,
            _giftSelectionService.Object,
            _freeshipCalculator.Object);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ValidPayload_ReturnsSalesOffer()
    {
        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 350000m });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(new Gift { Code = "GIFT_1", Name = "Sua rua mat mini" });
        _freeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(30000m);
        _freeshipCalculator.Setup(x => x.GetFreeshipMessage(false)).Returns("Phi van chuyen tam tinh la 30,000d.");

        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_KCN");

        Assert.Contains("San pham: Kem Chong Nang (350,000d)", result);
        Assert.Contains("Qua tang theo du lieu noi bo hien tai: Sua rua mat mini", result);
        Assert.Contains("chua dam chot freeship hay phi ship", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Phi van chuyen tam tinh la 30,000d.", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("so dien thoai", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ProductNotFound_ReturnsHelpfulError()
    {
        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_NOTFOUND"))
            .ReturnsAsync((Product?)null);

        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_NOTFOUND");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task HandlePostbackAsync_UsesSameSalesFormatting()
    {
        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_COMBO_2"))
            .ReturnsAsync(new Product { Code = "COMBO_2", Name = "Combo 2 San Pham", BasePrice = 600000m });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("COMBO_2"))
            .ReturnsAsync(new Gift { Code = "GIFT", Name = "Son duong moi" });
        _freeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(0m);
        _freeshipCalculator.Setup(x => x.GetFreeshipMessage(true)).Returns("Don nay duoc mien phi van chuyen.");

        var result = await _handler.HandlePostbackAsync("123", "PRODUCT_COMBO_2");

        Assert.Contains("Combo 2 San Pham", result);
        Assert.Contains("chua dam chot freeship hay phi ship", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Son duong moi", result);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_WithRememberedContact_ReturnsSoftConfirmationInsteadOfAskingAgain()
    {
        var stateMachine = new Mock<IStateMachine>();
        var rememberedContext = new StateContext
        {
            FacebookPSID = "123",
            CurrentState = ConversationState.Idle
        };
        rememberedContext.SetData("customerPhone", "0911111111");
        rememberedContext.SetData("shippingAddress", "22 Ly Tu Trong, Quan 1");
        rememberedContext.SetData("rememberedCustomerPhone", "0911111111");
        rememberedContext.SetData("rememberedShippingAddress", "22 Ly Tu Trong, Quan 1");
        rememberedContext.SetData("contactNeedsConfirmation", true);

        stateMachine
            .Setup(x => x.LoadOrCreateAsync("123", "PAGE_1"))
            .ReturnsAsync(rememberedContext);
        stateMachine
            .Setup(x => x.SaveAsync(rememberedContext))
            .Returns(Task.CompletedTask);

        var handler = new QuickReplyHandler(
            _productMappingService.Object,
            _giftSelectionService.Object,
            _freeshipCalculator.Object,
            stateMachine.Object,
            null,
            Mock.Of<ILogger<QuickReplyHandler>>());

        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 350000m });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(new Gift { Code = "GIFT_1", Name = "Qua mini" });
        _freeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(0m);
        _freeshipCalculator.Setup(x => x.GetFreeshipMessage(true)).Returns("Don nay duoc mien phi van chuyen.");

        var result = await handler.HandleQuickReplyAsync("123", "PRODUCT_KCN", "PAGE_1");

        Assert.Contains("lan truoc", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("so dien thoai va dia chi em len don luon nha.", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ShouldUseSafeShippingFallback_InsteadOfClaimingFreeshipPolicy()
    {
        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 320000m });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Serum duong da sample 5ml" });
        _freeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(30000m);

        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_MN");

        Assert.Contains("320,000d", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Serum duong da sample 5ml", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chua dam chot freeship hay phi ship", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30,000d.", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ShouldNotConfirmPrice_WhenProductHasVariantsButNoVariantSelected()
    {
        _productMappingService
            .Setup(x => x.GetProductByPayloadAsync("PRODUCT_KTN"))
            .ReturnsAsync(new Product
            {
                Code = "KTN",
                Name = "Kem Tri Nam",
                BasePrice = 520000m,
                Variants = new List<ProductVariant>
                {
                    new() { Id = "v1", VolumeML = 30, Texture = "cream", Price = 520000m, StockQuantity = 5, IsAvailable = true }
                }
            });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KTN"))
            .ReturnsAsync((Gift?)null);
        _freeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(30000m);

        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_KTN");

        Assert.Contains("gia em can kiem tra lai theo phien ban cu the", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("520,000d", result, StringComparison.OrdinalIgnoreCase);
    }
}
