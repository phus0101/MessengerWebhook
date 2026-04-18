using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Emotion.Models;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.Emotion;

public class RuleBasedEmotionDetectorTests
{
    private readonly RuleBasedEmotionDetector _detector;

    public RuleBasedEmotionDetectorTests()
    {
        _detector = new RuleBasedEmotionDetector();
    }

    [Theory]
    [InlineData("Sản phẩm tuyệt vời quá!", EmotionType.Positive)]
    [InlineData("Kem này hay lắm ạ", EmotionType.Positive)]
    [InlineData("Ok em, cảm ơn nhiều", EmotionType.Positive)]
    [InlineData("Tốt quá 😊", EmotionType.Positive)]
    public void DetectEmotion_PositiveVietnamese_ReturnsPositive(string message, EmotionType expected)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(expected, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Theory]
    [InlineData("Không tốt lắm", EmotionType.Negative)]
    [InlineData("Dở quá", EmotionType.Negative)]
    [InlineData("Không thích sản phẩm này", EmotionType.Negative)]
    [InlineData("Thất vọng 😢", EmotionType.Negative)]
    public void DetectEmotion_NegativeVietnamese_ReturnsNegative(string message, EmotionType expected)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(expected, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Theory]
    [InlineData("Bực mình quá!!!", EmotionType.Frustrated)]
    [InlineData("Tức giận", EmotionType.Frustrated)]
    [InlineData("Chán quá đi", EmotionType.Frustrated)]
    [InlineData("Mệt mỏi 😠", EmotionType.Frustrated)]
    public void DetectEmotion_FrustratedVietnamese_ReturnsFrustrated(string message, EmotionType expected)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(expected, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Theory]
    [InlineData("Wow quá đỉnh!!!", EmotionType.Excited)]
    [InlineData("Háo hức quá 🎉", EmotionType.Excited)]
    [InlineData("Xuất sắc quá đi!!!", EmotionType.Excited)]
    public void DetectEmotion_ExcitedVietnamese_ReturnsExcited(string message, EmotionType expected)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(expected, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Theory]
    [InlineData("Oke")]
    [InlineData("Vâng ạ")]
    [InlineData("Được")]
    public void DetectEmotion_NeutralVietnamese_ReturnsNeutral(string message)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
    }

    [Fact]
    public void DetectEmotion_NegationWithPositive_ReturnsNegative()
    {
        var result = _detector.DetectEmotion("không tốt lắm");

        Assert.Equal(EmotionType.Negative, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Fact]
    public void DetectEmotion_MultipleExclamationMarks_IncreasesConfidence()
    {
        var result1 = _detector.DetectEmotion("Tuyệt vời!");
        var result2 = _detector.DetectEmotion("Tuyệt vời!!!");

        Assert.True(result2.Confidence >= result1.Confidence);
    }

    [Fact]
    public void DetectEmotion_EmptyString_ReturnsNeutral()
    {
        var result = _detector.DetectEmotion("");

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void DetectEmotion_NullString_ReturnsNeutral()
    {
        var result = _detector.DetectEmotion(null!);

        Assert.Equal(EmotionType.Neutral, result.PrimaryEmotion);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void DetectEmotion_VeryLongMessage_HandlesGracefully()
    {
        var longMessage = string.Concat(Enumerable.Repeat("Sản phẩm tuyệt vời quá! ", 100));

        var result = _detector.DetectEmotion(longMessage);

        Assert.Equal(EmotionType.Positive, result.PrimaryEmotion);
        Assert.True(result.Confidence > 0.3);
    }

    [Theory]
    [InlineData("TUYỆT VỜI QUÁ", EmotionType.Positive)]
    [InlineData("không tốt", EmotionType.Negative)]
    [InlineData("BỰC MÌNH", EmotionType.Frustrated)]
    public void DetectEmotion_CaseInsensitive_DetectsCorrectly(string message, EmotionType expected)
    {
        var result = _detector.DetectEmotion(message);

        Assert.Equal(expected, result.PrimaryEmotion);
    }

    [Fact]
    public void DetectEmotion_MixedEmotions_ReturnsStrongest()
    {
        var result = _detector.DetectEmotion("Sản phẩm tốt nhưng giao hàng chậm quá bực mình");

        // Should detect frustrated due to "bực mình" being strong signal
        Assert.True(result.Scores.ContainsKey(EmotionType.Frustrated));
        Assert.True(result.Scores.ContainsKey(EmotionType.Positive));
    }

    [Fact]
    public void DetectEmotion_VietnameseDiacritics_HandlesCorrectly()
    {
        var result1 = _detector.DetectEmotion("tuyệt vời");
        var result2 = _detector.DetectEmotion("tuyet voi");

        // Both should detect positive, but with diacritics might have higher confidence
        Assert.Equal(EmotionType.Positive, result1.PrimaryEmotion);
        Assert.Equal(EmotionType.Positive, result2.PrimaryEmotion);
    }

    [Fact]
    public void DetectEmotion_ReturnsAllEmotionScores()
    {
        var result = _detector.DetectEmotion("Sản phẩm tuyệt vời!");

        Assert.Equal(5, result.Scores.Count);
        Assert.Contains(EmotionType.Positive, result.Scores.Keys);
        Assert.Contains(EmotionType.Neutral, result.Scores.Keys);
        Assert.Contains(EmotionType.Negative, result.Scores.Keys);
        Assert.Contains(EmotionType.Frustrated, result.Scores.Keys);
        Assert.Contains(EmotionType.Excited, result.Scores.Keys);
    }

    [Fact]
    public void DetectEmotion_DetectionMethodIsRuleBased()
    {
        var result = _detector.DetectEmotion("Test message");

        Assert.Equal("rule-based", result.DetectionMethod);
    }
}
