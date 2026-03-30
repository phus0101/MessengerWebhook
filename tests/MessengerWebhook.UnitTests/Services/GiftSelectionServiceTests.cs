using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.GiftSelection;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class GiftSelectionServiceTests
{
    private readonly Mock<IProductGiftMappingRepository> _mockMappingRepository;
    private readonly GiftSelectionService _service;

    public GiftSelectionServiceTests()
    {
        _mockMappingRepository = new Mock<IProductGiftMappingRepository>();
        _service = new GiftSelectionService(_mockMappingRepository.Object);
    }

    [Fact]
    public async Task SelectGiftForProductAsync_WithMapping_ReturnsFirstGift()
    {
        // Arrange
        var gift1 = new Gift { Code = "GIFT_1", Name = "Gift 1", IsActive = true };
        var gift2 = new Gift { Code = "GIFT_2", Name = "Gift 2", IsActive = true };
        var mappings = new List<ProductGiftMapping>
        {
            new() { ProductCode = "KCN", GiftCode = "GIFT_1", Priority = 1, Gift = gift1 },
            new() { ProductCode = "KCN", GiftCode = "GIFT_2", Priority = 2, Gift = gift2 }
        };

        _mockMappingRepository.Setup(r => r.GetByProductCodeAsync("KCN"))
            .ReturnsAsync(mappings);

        // Act
        var result = await _service.SelectGiftForProductAsync("KCN");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("GIFT_1", result.Code);
    }

    [Fact]
    public async Task SelectGiftForProductAsync_NoMapping_ReturnsNull()
    {
        // Arrange
        _mockMappingRepository.Setup(r => r.GetByProductCodeAsync("NOTFOUND"))
            .ReturnsAsync(new List<ProductGiftMapping>());

        // Act
        var result = await _service.SelectGiftForProductAsync("NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvailableGiftsForProductAsync_WithMappings_ReturnsAllGifts()
    {
        // Arrange
        var gift1 = new Gift { Code = "GIFT_1", Name = "Gift 1", IsActive = true };
        var gift2 = new Gift { Code = "GIFT_2", Name = "Gift 2", IsActive = true };
        var mappings = new List<ProductGiftMapping>
        {
            new() { ProductCode = "KL", GiftCode = "GIFT_1", Priority = 1, Gift = gift1 },
            new() { ProductCode = "KL", GiftCode = "GIFT_2", Priority = 2, Gift = gift2 }
        };

        _mockMappingRepository.Setup(r => r.GetByProductCodeAsync("KL"))
            .ReturnsAsync(mappings);

        // Act
        var result = await _service.GetAvailableGiftsForProductAsync("KL");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Code == "GIFT_1");
        Assert.Contains(result, g => g.Code == "GIFT_2");
    }

    [Fact]
    public async Task GetAvailableGiftsForProductAsync_NoMappings_ReturnsEmptyList()
    {
        // Arrange
        _mockMappingRepository.Setup(r => r.GetByProductCodeAsync("NOTFOUND"))
            .ReturnsAsync(new List<ProductGiftMapping>());

        // Act
        var result = await _service.GetAvailableGiftsForProductAsync("NOTFOUND");

        // Assert
        Assert.Empty(result);
    }
}
