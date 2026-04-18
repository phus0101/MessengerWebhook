using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SmallTalk.Models;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.SmallTalk;

public class SmallTalkDetectorTests
{
    private readonly SmallTalkDetector _detector;

    public SmallTalkDetectorTests()
    {
        _detector = new SmallTalkDetector();
    }

    [Theory]
    [InlineData("hi", SmallTalkIntent.Greeting)]
    [InlineData("hello", SmallTalkIntent.Greeting)]
    [InlineData("chào", SmallTalkIntent.Greeting)]
    [InlineData("alo", SmallTalkIntent.Greeting)]
    [InlineData("hi shop", SmallTalkIntent.Greeting)]
    [InlineData("chào shop", SmallTalkIntent.Greeting)]
    [InlineData("xin chào", SmallTalkIntent.Greeting)]
    public void DetectIntent_GreetingKeywords_ReturnsGreeting(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("có ai không", SmallTalkIntent.CheckIn)]
    [InlineData("shop ơi", SmallTalkIntent.CheckIn)]
    [InlineData("có shop không", SmallTalkIntent.CheckIn)]
    [InlineData("còn bán không", SmallTalkIntent.CheckIn)]
    [InlineData("mở cửa không", SmallTalkIntent.CheckIn)]
    public void DetectIntent_CheckInKeywords_ReturnsCheckIn(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("cảm ơn", SmallTalkIntent.Pleasantry)]
    [InlineData("thanks", SmallTalkIntent.Pleasantry)]
    [InlineData("thank you", SmallTalkIntent.Pleasantry)]
    public void DetectIntent_PleasantryKeywords_ReturnsPleasantry(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ok", SmallTalkIntent.Acknowledgment)]
    [InlineData("oke", SmallTalkIntent.Acknowledgment)]
    [InlineData("được", SmallTalkIntent.Acknowledgment)]
    [InlineData("uhm", SmallTalkIntent.Acknowledgment)]
    public void DetectIntent_ShortAcknowledgments_ReturnsAcknowledgment(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("mua sản phẩm", SmallTalkIntent.None)]
    [InlineData("giá bao nhiêu", SmallTalkIntent.None)]
    [InlineData("đặt hàng", SmallTalkIntent.None)]
    [InlineData("ship về đâu", SmallTalkIntent.None)]
    [InlineData("tư vấn sản phẩm", SmallTalkIntent.None)]
    public void DetectIntent_BusinessKeywords_ReturnsNone(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectIntent_EmptyMessage_ReturnsNone()
    {
        // Act
        var result = _detector.DetectIntent("");

        // Assert
        Assert.Equal(SmallTalkIntent.None, result);
    }

    [Fact]
    public void DetectIntent_WhitespaceOnly_ReturnsNone()
    {
        // Act
        var result = _detector.DetectIntent("   ");

        // Assert
        Assert.Equal(SmallTalkIntent.None, result);
    }

    [Theory]
    [InlineData("HI", SmallTalkIntent.Greeting)]
    [InlineData("CHÀO SHOP", SmallTalkIntent.Greeting)]
    [InlineData("CÓ AI KHÔNG", SmallTalkIntent.CheckIn)]
    public void DetectIntent_UpperCase_DetectsCorrectly(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hi shop, mua sản phẩm", SmallTalkIntent.None)] // Business takes precedence
    [InlineData("chào, giá bao nhiêu", SmallTalkIntent.None)] // Business takes precedence
    public void DetectIntent_MixedGreetingAndBusiness_BusinessTakesPrecedence(string message, SmallTalkIntent expected)
    {
        // Act
        var result = _detector.DetectIntent(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateConfidence_GreetingIntent_ReturnsHighConfidence()
    {
        // Arrange
        var message = "hi shop";
        var intent = SmallTalkIntent.Greeting;

        // Act
        var confidence = _detector.CalculateConfidence(message, intent);

        // Assert
        Assert.True(confidence >= 0.8); // 2 words = 0.85 confidence
    }

    [Fact]
    public void CalculateConfidence_NoneIntent_ReturnsZero()
    {
        // Arrange
        var message = "mua sản phẩm";
        var intent = SmallTalkIntent.None;

        // Act
        var confidence = _detector.CalculateConfidence(message, intent);

        // Assert
        Assert.Equal(0.0, confidence);
    }

    [Fact]
    public void IsBusinessIntent_ProductKeyword_ReturnsTrue()
    {
        // Act
        var result = _detector.IsBusinessIntent("xem sản phẩm");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsBusinessIntent_PureGreeting_ReturnsFalse()
    {
        // Act
        var result = _detector.IsBusinessIntent("hi shop");

        // Assert
        Assert.False(result);
    }
}
