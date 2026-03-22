using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Strategies;
using Xunit;
using FluentAssertions;

namespace MessengerWebhook.UnitTests.Services.AI.Strategies;

public class HybridModelSelectionStrategyTests
{
    private readonly HybridModelSelectionStrategy _strategy;

    public HybridModelSelectionStrategyTests()
    {
        _strategy = new HybridModelSelectionStrategy();
    }

    [Theory]
    [InlineData("xin chào")]
    [InlineData("hello")]
    [InlineData("giá bao nhiêu?")]
    [InlineData("có màu gì?")]
    public void SelectModel_SimpleShortMessage_ReturnsFlashLite(string message)
    {
        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.FlashLite);
    }

    [Theory]
    [InlineData("tư vấn cho tôi sản phẩm phù hợp")]
    [InlineData("gợi ý kem dưỡng da cho da khô")]
    [InlineData("nên mặc gì đi dự tiệc?")]
    [InlineData("đề xuất sản phẩm cho tôi")]
    [InlineData("recommend a product for me")]
    [InlineData("suggest something")]
    [InlineData("I need advice")]
    [InlineData("giúp tôi chọn sản phẩm")]
    public void SelectModel_ComplexKeywords_ReturnsPro(string message)
    {
        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.Pro);
    }

    [Theory]
    [InlineData("so sánh hai sản phẩm này")]
    [InlineData("khác nhau giữa A và B")]
    [InlineData("compare these products")]
    public void SelectModel_ComparisonKeywords_ReturnsPro(string message)
    {
        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_LongMessage_ReturnsPro()
    {
        // Arrange - Message > 100 characters
        var longMessage = new string('a', 101);

        // Act
        var result = _strategy.SelectModel(longMessage);

        // Assert
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_ExactlyHundredChars_ReturnsFlashLite()
    {
        // Arrange - Message exactly 100 characters
        var message = new string('a', 100);

        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.FlashLite);
    }

    [Theory]
    [InlineData("TƯ VẤN cho tôi")]
    [InlineData("GỢI Ý sản phẩm")]
    [InlineData("RECOMMEND something")]
    public void SelectModel_KeywordsCaseInsensitive_ReturnsPro(string message)
    {
        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.Pro);
    }

    [Fact]
    public void SelectModel_EmptyString_ReturnsFlashLite()
    {
        // Act
        var result = _strategy.SelectModel(string.Empty);

        // Assert
        result.Should().Be(GeminiModelType.FlashLite);
    }

    [Theory]
    [InlineData("có sản phẩm nào tư vấn được không?")]
    [InlineData("shop có gợi ý gì không?")]
    public void SelectModel_KeywordInMiddle_ReturnsPro(string message)
    {
        // Act
        var result = _strategy.SelectModel(message);

        // Assert
        result.Should().Be(GeminiModelType.Pro);
    }
}
