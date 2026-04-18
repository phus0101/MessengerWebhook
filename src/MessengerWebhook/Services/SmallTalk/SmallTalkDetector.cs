using MessengerWebhook.Services.SmallTalk.Models;

namespace MessengerWebhook.Services.SmallTalk;

/// <summary>
/// Detects small talk intent using Vietnamese keyword matching
/// </summary>
public class SmallTalkDetector
{
    private static readonly HashSet<string> GreetingKeywords = new()
    {
        "hi", "hello", "chào", "alo", "alô", "xin chào", "chào shop",
        "hi shop", "hi sốp", "alo shop", "chào bạn", "helo", "hê lô"
    };

    private static readonly HashSet<string> CheckInKeywords = new()
    {
        "có ai", "có người", "shop ơi", "có shop", "có bạn",
        "còn bán", "còn hoạt động", "mở cửa", "có mở"
    };

    private static readonly HashSet<string> PleasantryKeywords = new()
    {
        "trời đẹp", "buổi sáng", "buổi chiều", "buổi tối", "chúc",
        "cảm ơn", "thanks", "thank you", "tks", "cám ơn"
    };

    private static readonly HashSet<string> BusinessKeywords = new()
    {
        "sản phẩm", "mua", "đặt", "giá", "bao nhiêu", "ship",
        "giao hàng", "order", "đơn hàng", "tư vấn", "xem",
        "có gì", "có loại", "có màu", "size", "còn hàng"
    };

    private static readonly HashSet<string> AcknowledgmentKeywords = new()
    {
        "ok", "oke", "okay", "được", "uhm", "uh", "à", "vâng", "dạ"
    };

    /// <summary>
    /// Detects small talk intent from message text
    /// </summary>
    public SmallTalkIntent DetectIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return SmallTalkIntent.None;

        var normalized = message.ToLower().Trim();

        // Business keywords take precedence - if detected, not small talk
        if (BusinessKeywords.Any(k => normalized.Contains(k)))
            return SmallTalkIntent.None;

        // Check greeting patterns (must be at start or standalone)
        if (GreetingKeywords.Any(k => normalized.StartsWith(k) || normalized == k))
            return SmallTalkIntent.Greeting;

        // Check check-in patterns
        if (CheckInKeywords.Any(k => normalized.Contains(k)))
            return SmallTalkIntent.CheckIn;

        // Check pleasantries
        if (PleasantryKeywords.Any(k => normalized.Contains(k)))
            return SmallTalkIntent.Pleasantry;

        // Short acknowledgments (must be short and match exactly)
        if (normalized.Length <= 5 && AcknowledgmentKeywords.Contains(normalized))
            return SmallTalkIntent.Acknowledgment;

        return SmallTalkIntent.None;
    }

    /// <summary>
    /// Calculates confidence score for detected intent
    /// </summary>
    public double CalculateConfidence(string message, SmallTalkIntent intent)
    {
        if (intent == SmallTalkIntent.None)
            return 0.0;

        var normalized = message.ToLower().Trim();
        var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // High confidence for exact matches
        if (wordCount == 1 && GreetingKeywords.Contains(normalized))
            return 1.0;

        if (wordCount <= 3 && CheckInKeywords.Any(k => normalized.Contains(k)))
            return 0.95;

        // Medium confidence for longer messages with small talk keywords
        if (wordCount <= 5)
            return 0.85;

        // Lower confidence for longer messages (might have mixed intent)
        if (wordCount <= 10)
            return 0.75;

        return 0.6;
    }

    /// <summary>
    /// Checks if message contains business intent keywords
    /// </summary>
    public bool IsBusinessIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var normalized = message.ToLower().Trim();
        return BusinessKeywords.Any(k => normalized.Contains(k));
    }
}
