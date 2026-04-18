using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.ProductMapping;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class ProductMappingServiceTests
{
    private readonly Mock<IProductRepository> _mockProductRepository;
    private readonly ProductMappingService _service;

    public ProductMappingServiceTests()
    {
        _mockProductRepository = new Mock<IProductRepository>();
        _service = new ProductMappingService(_mockProductRepository.Object);
    }

    [Theory]
    [InlineData("PRODUCT_KCN", true)]
    [InlineData("PRODUCT_KL", true)]
    [InlineData("PRODUCT_COMBO_2", true)]
    [InlineData("PRODUCT_", false)]
    [InlineData("KCN", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPayload_ShouldValidateCorrectly(string? payload, bool expected)
    {
        // Act
        var result = _service.IsValidPayload(payload!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_ValidPayload_ReturnsProduct()
    {
        // Arrange
        var product = new Product { Id = "1", Code = "KCN", Name = "Kem Chống Nắng", IsActive = true };
        _mockProductRepository.Setup(r => r.GetByCodeAsync("KCN"))
            .ReturnsAsync(product);

        // Act
        var result = await _service.GetProductByPayloadAsync("PRODUCT_KCN");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("KCN", result.Code);
        Assert.Equal("Kem Chống Nắng", result.Name);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_InvalidPayload_ReturnsNull()
    {
        // Act
        var result = await _service.GetProductByPayloadAsync("INVALID");

        // Assert
        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetByCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_ProductNotFound_ReturnsNull()
    {
        // Arrange
        _mockProductRepository.Setup(r => r.GetByCodeAsync("NOTFOUND"))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _service.GetProductByPayloadAsync("PRODUCT_NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProductByCodeAsync_ExistingCode_ReturnsProduct()
    {
        // Arrange
        var product = new Product { Id = "1", Code = "KL", Name = "Kem Lụa", IsActive = true };
        _mockProductRepository.Setup(r => r.GetByCodeAsync("KL"))
            .ReturnsAsync(product);

        // Act
        var result = await _service.GetProductByCodeAsync("KL");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("KL", result.Code);
    }

    [Fact]
    public async Task GetProductByCodeAsync_NonExistingCode_ReturnsNull()
    {
        // Arrange
        _mockProductRepository.Setup(r => r.GetByCodeAsync("NOTFOUND"))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _service.GetProductByCodeAsync("NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProductByMessageAsync_ExplicitCombo2_ReturnsComboProduct()
    {
        var product = new Product { Id = "combo-2", Code = "COMBO_2", Name = "Combo 2", IsActive = true };
        _mockProductRepository.Setup(r => r.GetByCodeAsync("COMBO_2"))
            .ReturnsAsync(product);

        var result = await _service.GetProductByMessageAsync("combo 2");

        Assert.NotNull(result);
        Assert.Equal("COMBO_2", result.Code);
    }

    [Theory]
    [InlineData("freeship")]
    [InlineData("2 sản phẩm")]
    [InlineData("combo")]
    public async Task GetProductByMessageAsync_AmbiguousPromoPhrases_DoNotAutoMapToCombo2(string message)
    {
        var result = await _service.GetProductByMessageAsync(message);

        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetByCodeAsync("COMBO_2"), Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_MatNaNguDuongAm_ShouldPreferMaskOverKemLua()
    {
        var product = new Product { Id = "mn-1", Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", IsActive = true };
        _mockProductRepository.Setup(r => r.GetByCodeAsync("MN"))
            .ReturnsAsync(product);

        var result = await _service.GetProductByMessageAsync("Mặt Nạ Ngủ Dưỡng Ẩm");

        Assert.NotNull(result);
        Assert.Equal("MN", result!.Code);
        _mockProductRepository.Verify(r => r.GetByCodeAsync("KL"), Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_DuongAmOnly_ShouldNotAutoMapToKemLua()
    {
        var result = await _service.GetProductByMessageAsync("tìm sản phẩm dưỡng ẩm");

        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetByCodeAsync("KL"), Times.Never);
    }
}
