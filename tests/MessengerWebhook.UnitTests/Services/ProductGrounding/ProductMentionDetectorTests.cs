using MessengerWebhook.Services.ProductGrounding;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.ProductGrounding;

public class ProductMentionDetectorTests
{
    private readonly ProductMentionDetector _detector = new();

    [Fact]
    public void ExtractProductMentions_HallucinatedMaskName_ReturnsMention()
    {
        var mentions = _detector.ExtractProductMentions("Bên em có dòng mặt nạ Mặt nạ Tảo Biển Tươi Múi Xù rất tốt ạ.");

        Assert.Contains("Mặt nạ Tảo Biển Tươi Múi Xù", mentions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractProductMentions_HallucinatedNameWithoutCategoryPrefix_ReturnsMention()
    {
        var mentions = _detector.ExtractProductMentions("Dạ Tảo Biển Tươi Múi Xù cấp ẩm rất tốt ạ.");

        Assert.Contains("Tảo Biển Tươi Múi Xù", mentions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractProductMentions_LowercaseSpecificNameWithProductContext_ReturnsMention()
    {
        var mentions = _detector.ExtractProductMentions("Dạ bên em có tảo biển tươi múi xù cấp ẩm tốt ạ.");

        Assert.Contains("tảo biển tươi múi xù", mentions, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Dạ chị thử tảo biển tươi múi xù nha.")]
    [InlineData("Em nghĩ tảo biển tươi múi xù hợp da chị ạ.")]
    public void ExtractProductMentions_LowercaseSpecificRecommendation_ReturnsMention(string response)
    {
        var mentions = _detector.ExtractProductMentions(response);

        Assert.Contains("tảo biển tươi múi xù", mentions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractProductMentions_GenericNeedPhrase_ReturnsEmpty()
    {
        var mentions = _detector.ExtractProductMentions("chị đang tìm mặt nạ dưỡng ẩm ạ");

        Assert.Empty(mentions);
    }

    [Fact]
    public void ExtractProductMentions_GenericRecommendationPhrase_ReturnsEmpty()
    {
        var mentions = _detector.ExtractProductMentions("Dạ sản phẩm này rất tốt cho da chị ạ. Em nghĩ chị sẽ thích lắm.");

        Assert.Empty(mentions);
    }
}
