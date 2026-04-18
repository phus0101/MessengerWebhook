using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Conversation.Models;

namespace MessengerWebhook.Services.Conversation;

/// <summary>
/// Detects conversation patterns like repeat questions, topic shifts, buying signals
/// </summary>
public class PatternDetector
{
    private static readonly HashSet<string> BuyingSignals = new()
    {
        "đặt", "mua", "chốt", "lấy", "gửi", "order", "đặt hàng", "mua luôn",
        "chốt đơn", "lấy luôn", "gửi cho em", "đặt ngay"
    };

    private static readonly HashSet<string> HesitationSignals = new()
    {
        "suy nghĩ", "xem thêm", "chưa chắc", "để em", "hơi đắt", "đắt quá",
        "suy nghĩ thêm", "để em xem", "chưa quyết định", "cân nhắc"
    };

    private static readonly HashSet<string> PriceKeywords = new()
    {
        "giá", "bao nhiêu", "tiền", "đắt", "rẻ", "giảm", "khuyến mãi",
        "giá bao nhiêu", "bao nhiêu tiền", "giá cả", "chi phí"
    };

    /// <summary>
    /// Detect all patterns in conversation history
    /// </summary>
    public List<ConversationPattern> DetectPatterns(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();

        if (history.Count == 0)
            return patterns;

        patterns.AddRange(DetectRepeatQuestions(history));
        patterns.AddRange(DetectTopicShifts(history));
        patterns.AddRange(DetectBuyingSignals(history));
        patterns.AddRange(DetectHesitation(history));
        patterns.AddRange(DetectPriceSensitivity(history));
        patterns.AddRange(DetectEngagementDrop(history));

        return patterns;
    }

    /// <summary>
    /// Detect repeat questions within window
    /// </summary>
    private List<ConversationPattern> DetectRepeatQuestions(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var userMessages = history
            .Select((msg, idx) => new { Message = msg, Index = idx })
            .Where(x => x.Message.Role == "user")
            .ToList();

        for (int i = 0; i < userMessages.Count; i++)
        {
            var turnIndices = new List<int> { userMessages[i].Index };
            var baseContent = userMessages[i].Message.Content;

            // Look ahead for similar questions
            for (int j = i + 1; j < userMessages.Count && j <= i + 5; j++)
            {
                var similarity = CalculateSimilarity(baseContent, userMessages[j].Message.Content);
                if (similarity >= 0.8)
                {
                    turnIndices.Add(userMessages[j].Index);
                }
            }

            if (turnIndices.Count > 1)
            {
                patterns.Add(new ConversationPattern
                {
                    Type = PatternType.RepeatQuestion,
                    Occurrences = turnIndices.Count,
                    TurnIndices = turnIndices,
                    Confidence = 0.9,
                    Description = "Customer repeated similar question"
                });
                break; // Only report first repeat pattern
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detect sudden topic changes
    /// </summary>
    private List<ConversationPattern> DetectTopicShifts(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var userMessages = history
            .Select((msg, idx) => new { Message = msg, Index = idx })
            .Where(x => x.Message.Role == "user")
            .ToList();

        for (int i = 1; i < userMessages.Count; i++)
        {
            var prevContent = userMessages[i - 1].Message.Content;
            var currContent = userMessages[i].Message.Content;
            var similarity = CalculateSimilarity(prevContent, currContent);

            // Low similarity indicates topic shift
            if (similarity < 0.3 && prevContent.Length > 10 && currContent.Length > 10)
            {
                patterns.Add(new ConversationPattern
                {
                    Type = PatternType.TopicShift,
                    Occurrences = 1,
                    TurnIndices = new List<int> { userMessages[i].Index },
                    Confidence = 1.0 - similarity,
                    Description = "Sudden topic change detected"
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detect buying signals in messages
    /// </summary>
    private List<ConversationPattern> DetectBuyingSignals(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var turnIndices = new List<int>();

        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].Role != "user")
                continue;

            var content = history[i].Content.ToLower();
            var signalCount = BuyingSignals.Count(signal => content.Contains(signal));

            if (signalCount > 0)
            {
                turnIndices.Add(i);
            }
        }

        if (turnIndices.Count > 0)
        {
            patterns.Add(new ConversationPattern
            {
                Type = PatternType.BuyingSignal,
                Occurrences = turnIndices.Count,
                TurnIndices = turnIndices,
                Confidence = Math.Min(0.9, 0.5 + (turnIndices.Count * 0.2)),
                Description = "Customer showing purchase intent"
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detect hesitation patterns
    /// </summary>
    private List<ConversationPattern> DetectHesitation(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var turnIndices = new List<int>();

        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].Role != "user")
                continue;

            var content = history[i].Content.ToLower();
            var hesitationCount = HesitationSignals.Count(signal => content.Contains(signal));

            if (hesitationCount > 0)
            {
                turnIndices.Add(i);
            }
        }

        if (turnIndices.Count > 0)
        {
            patterns.Add(new ConversationPattern
            {
                Type = PatternType.Hesitation,
                Occurrences = turnIndices.Count,
                TurnIndices = turnIndices,
                Confidence = Math.Min(0.9, 0.6 + (turnIndices.Count * 0.15)),
                Description = "Customer showing hesitation or uncertainty"
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detect price sensitivity
    /// </summary>
    private List<ConversationPattern> DetectPriceSensitivity(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var turnIndices = new List<int>();

        for (int i = 0; i < history.Count; i++)
        {
            if (history[i].Role != "user")
                continue;

            var content = history[i].Content.ToLower();
            var priceCount = PriceKeywords.Count(keyword => content.Contains(keyword));

            if (priceCount > 0)
            {
                turnIndices.Add(i);
            }
        }

        // Price sensitivity if mentioned 2+ times
        if (turnIndices.Count >= 2)
        {
            patterns.Add(new ConversationPattern
            {
                Type = PatternType.PriceSensitivity,
                Occurrences = turnIndices.Count,
                TurnIndices = turnIndices,
                Confidence = Math.Min(0.95, 0.5 + (turnIndices.Count * 0.15)),
                Description = "Customer sensitive about pricing"
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detect engagement drop based on message length
    /// </summary>
    private List<ConversationPattern> DetectEngagementDrop(List<ConversationMessage> history)
    {
        var patterns = new List<ConversationPattern>();
        var userMessages = history
            .Select((msg, idx) => new { Message = msg, Index = idx })
            .Where(x => x.Message.Role == "user")
            .ToList();

        if (userMessages.Count < 3)
            return patterns;

        // Calculate average length of first half vs second half
        var midPoint = userMessages.Count / 2;
        var firstHalfAvg = userMessages.Take(midPoint).Average(x => x.Message.Content.Length);
        var secondHalfAvg = userMessages.Skip(midPoint).Average(x => x.Message.Content.Length);

        // Engagement drop if second half is significantly shorter
        if (secondHalfAvg < firstHalfAvg * 0.5 && firstHalfAvg > 20)
        {
            patterns.Add(new ConversationPattern
            {
                Type = PatternType.EngagementDrop,
                Occurrences = 1,
                TurnIndices = userMessages.Skip(midPoint).Select(x => x.Index).ToList(),
                Confidence = 0.7,
                Description = "Customer engagement decreasing"
            });
        }

        return patterns;
    }

    /// <summary>
    /// Calculate similarity between two messages using word overlap
    /// </summary>
    private double CalculateSimilarity(string msg1, string msg2)
    {
        if (string.IsNullOrWhiteSpace(msg1) || string.IsNullOrWhiteSpace(msg2))
            return 0;

        var words1 = msg1.ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();
        var words2 = msg2.ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        if (words1.Count == 0 || words2.Count == 0)
            return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0;
    }
}
