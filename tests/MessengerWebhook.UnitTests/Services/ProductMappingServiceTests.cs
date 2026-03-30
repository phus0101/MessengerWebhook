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
    public void IsValidPayload_ShouldValidateCorrectly(string payload, bool expected)
    {
        // Act
        var result = _service.IsValidPayload(payload);

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
}
