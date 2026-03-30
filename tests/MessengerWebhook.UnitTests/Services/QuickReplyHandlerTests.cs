using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.QuickReply;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class QuickReplyHandlerTests
{
    private readonly Mock<IProductMappingService> _mockProductMappingService;
    private readonly Mock<IGiftSelectionService> _mockGiftSelectionService;
    private readonly Mock<IFreeshipCalculator> _mockFreeshipCalculator;
    private readonly QuickReplyHandler _handler;

    public QuickReplyHandlerTests()
    {
        _mockProductMappingService = new Mock<IProductMappingService>();
        _mockGiftSelectionService = new Mock<IGiftSelectionService>();
        _mockFreeshipCalculator = new Mock<IFreeshipCalculator>();
        _handler = new QuickReplyHandler(
            _mockProductMappingService.Object,
            _mockGiftSelectionService.Object,
            _mockFreeshipCalculator.Object);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ValidPayload_ReturnsFormattedMessage()
    {
        // Arrange
        var product = new Product { Code = "KCN", Name = "Kem Chống Nắng", BasePrice = 350000 };
        var gift = new Gift { Code = "GIFT_1", Name = "Sữa rửa mặt mini" };

        _mockProductMappingService.Setup(s => s.GetProductByPayloadAsync("PRODUCT_KCN"))
            .ReturnsAsync(product);
        _mockGiftSelectionService.Setup(s => s.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(gift);
        _mockFreeshipCalculator.Setup(c => c.IsEligibleForFreeship(It.IsAny<List<string>>()))
            .Returns(false);
        _mockFreeshipCalculator.Setup(c => c.GetFreeshipMessage(false))
            .Returns("(Phí vận chuyển: 30.000đ)");

        // Act
        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_KCN");

        // Assert
        Assert.Contains("Kem Chống Nắng", result);
        Assert.Contains("350.000đ", result);
        Assert.Contains("Sữa rửa mặt mini", result);
        Assert.Contains("Phí vận chuyển", result);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_ProductNotFound_ReturnsErrorMessage()
    {
        // Arrange
        _mockProductMappingService.Setup(s => s.GetProductByPayloadAsync("PRODUCT_NOTFOUND"))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_NOTFOUND");

        // Assert
        Assert.Contains("không tìm thấy sản phẩm", result);
    }

    [Fact]
    public async Task HandleQuickReplyAsync_NoGift_ReturnsMessageWithoutGift()
    {
        // Arrange
        var product = new Product { Code = "KL", Name = "Kem Lụa", BasePrice = 280000 };

        _mockProductMappingService.Setup(s => s.GetProductByPayloadAsync("PRODUCT_KL"))
            .ReturnsAsync(product);
        _mockGiftSelectionService.Setup(s => s.SelectGiftForProductAsync("KL"))
            .ReturnsAsync((Gift?)null);
        _mockFreeshipCalculator.Setup(c => c.IsEligibleForFreeship(It.IsAny<List<string>>()))
            .Returns(false);
        _mockFreeshipCalculator.Setup(c => c.GetFreeshipMessage(false))
            .Returns("(Phí vận chuyển: 30.000đ)");

        // Act
        var result = await _handler.HandleQuickReplyAsync("123", "PRODUCT_KL");

        // Assert
        Assert.Contains("Kem Lụa", result);
        Assert.DoesNotContain("🎁", result);
    }

    [Fact]
    public async Task HandlePostbackAsync_ValidPayload_ReturnsFormattedMessage()
    {
        // Arrange
        var product = new Product { Code = "COMBO_2", Name = "Combo 2 Sản Phẩm", BasePrice = 600000 };
        var gift = new Gift { Code = "GIFT_LIPBALM", Name = "Son dưỡng môi" };

        _mockProductMappingService.Setup(s => s.GetProductByPayloadAsync("PRODUCT_COMBO_2"))
            .ReturnsAsync(product);
        _mockGiftSelectionService.Setup(s => s.SelectGiftForProductAsync("COMBO_2"))
            .ReturnsAsync(gift);
        _mockFreeshipCalculator.Setup(c => c.IsEligibleForFreeship(It.IsAny<List<string>>()))
            .Returns(true);
        _mockFreeshipCalculator.Setup(c => c.GetFreeshipMessage(true))
            .Returns("(Miễn phí vận chuyển)");

        // Act
        var result = await _handler.HandlePostbackAsync("123", "PRODUCT_COMBO_2");

        // Assert
        Assert.Contains("Combo 2 Sản Phẩm", result);
        Assert.Contains("Miễn phí vận chuyển", result);
        Assert.Contains("Son dưỡng môi", result);
    }
}
