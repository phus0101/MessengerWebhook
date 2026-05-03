using MessengerWebhook.Services.ProductGrounding;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.ProductGrounding;

public class ProductNeedDetectorTests
{
    private readonly ProductNeedDetector _detector = new();

    [Theory]
    [InlineData("mặt nạ dưỡng ẩm")]
    [InlineData("shop có mặt nạ nào cấp ẩm không?")]
    [InlineData("serum trị nám bên em có loại nào")]
    public void RequiresProductGrounding_ProductNeedMessage_ReturnsTrue(string message)
    {
        Assert.True(_detector.RequiresProductGrounding(message));
    }

    [Fact]
    public void RequiresProductGrounding_NoProductNeed_ReturnsFalse()
    {
        Assert.False(_detector.RequiresProductGrounding("chị muốn hỏi phí ship"));
    }
}
