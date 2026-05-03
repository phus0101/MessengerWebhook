using FluentAssertions;
using MessengerWebhook.Services.SubIntent;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.SubIntent;

public class KeywordSubIntentDetectorTests
{
    private readonly KeywordSubIntentDetector _detector = new();

    [Theory]
    [InlineData("sản phẩm này có chứa paraben không?", SubIntentCategory.ProductQuestion)]
    [InlineData("thành phần có gì?", SubIntentCategory.ProductQuestion)]
    [InlineData("cách dùng như thế nào?", SubIntentCategory.ProductQuestion)]
    [InlineData("công dụng là gì?", SubIntentCategory.ProductQuestion)]
    [InlineData("nói thêm về sản phẩm", SubIntentCategory.ProductQuestion)]
    public async Task Detect_ProductQuestion_ReturnsCorrectCategory(string message, SubIntentCategory expected)
    {
        var result = await _detector.ClassifyAsync(message);
        result.Should().NotBeNull();
        result!.Category.Should().Be(expected);
        result.Confidence.Should().BeGreaterThan(0.6m);
        result.Source.Should().Be("keyword");
    }

    [Theory]
    [InlineData("giá bao nhiêu?", SubIntentCategory.PriceQuestion)]
    [InlineData("bao nhiu tiền?", SubIntentCategory.PriceQuestion)]
    [InlineData("có giảm giá không?", SubIntentCategory.PriceQuestion)]
    public async Task Detect_PriceQuestion_HandlesInformalSpelling(string message, SubIntentCategory expected)
    {
        var result = await _detector.ClassifyAsync(message);
        result.Should().NotBeNull();
        result!.Category.Should().Be(expected);
    }

    [Theory]
    [InlineData("ship mất bao lâu?", SubIntentCategory.ShippingQuestion)]
    [InlineData("có freeship không?", SubIntentCategory.ShippingQuestion)]
    public async Task Detect_ShippingQuestion_ReturnsCorrectCategory(string message, SubIntentCategory expected)
    {
        var result = await _detector.ClassifyAsync(message);
        result.Should().NotBeNull();
        result!.Category.Should().Be(expected);
    }

    [Fact]
    public async Task Detect_MultipleKeywords_ReturnsHighConfidence()
    {
        var message = "giá bao nhiêu và có giảm giá không?";
        var result = await _detector.ClassifyAsync(message);
        result.Should().NotBeNull();
        result!.Confidence.Should().BeGreaterThanOrEqualTo(0.9m);
    }

    [Fact]
    public async Task Detect_EmptyMessage_ReturnsNull()
    {
        var result = await _detector.ClassifyAsync("");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Detect_NoKeywordMatch_ReturnsNull()
    {
        var result = await _detector.ClassifyAsync("em muốn mua");
        result.Should().BeNull();
    }
}
