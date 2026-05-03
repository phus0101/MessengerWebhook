namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Fast keyword-based sub-intent detector (<10ms latency)
/// Handles 70% of queries with high confidence before escalating to AI
/// </summary>
public sealed class KeywordSubIntentDetector : ISubIntentClassifier
{
    private static readonly Dictionary<SubIntentCategory, HashSet<string>> Keywords = new()
    {
        [SubIntentCategory.ProductQuestion] = new()
        {
            // Features
            "công dụng", "tác dụng", "hiệu quả", "dùng để", "làm gì",
            "nói thêm", "noi them", "nói kỹ", "noi ky", "chi tiết", "chi tiet",
            // Ingredients
            "thành phần", "có gì", "chứa", "ingredient", "công thức",
            "có paraben", "có hóa chất", "tự nhiên",
            // Usage
            "cách dùng", "dùng như thế nào", "sử dụng", "thoa", "bôi", "apply",
            "dùng khi nào", "dùng buổi nào", "dùng sáng", "dùng tối"
        },

        [SubIntentCategory.PriceQuestion] = new()
        {
            "giá", "bao nhiêu", "tiền", "đắt", "rẻ", "giá cả", "chi phí",
            "giá bao nhiêu", "giá bn", "giá bnh", "bao nhiu", "bn tiền",
            "giảm giá", "sale", "khuyến mãi", "km", "voucher", "mã giảm"
        },

        [SubIntentCategory.ShippingQuestion] = new()
        {
            "giao", "ship", "vận chuyển", "nhận hàng", "giao hàng", "chuyển phát",
            "ship mất bao lâu", "bao lâu nhận", "khi nào nhận", "giao bao lâu",
            "phí ship", "freeship", "free ship", "miễn phí ship", "ship cod",
            "tracking", "mã vận đơn", "tra cứu đơn"
        },

        [SubIntentCategory.PolicyQuestion] = new()
        {
            "đổi trả", "hoàn tiền", "bảo hành", "chính sách", "policy",
            "đổi hàng", "trả hàng", "refund", "hoàn lại", "đền bù",
            "quà tặng", "tặng kèm", "gift", "bonus", "khuyến mãi kèm"
        },

        [SubIntentCategory.AvailabilityQuestion] = new()
        {
            "còn hàng", "còn không", "còn ko", "hết hàng", "out of stock",
            "có sẵn", "có hàng", "tồn kho", "availability", "in stock",
            "còn size", "còn màu", "còn mùi"
        },

        [SubIntentCategory.ComparisonQuestion] = new()
        {
            "so sánh", "khác gì", "khác nhau", "compare", "difference",
            "tốt hơn", "hơn", "vs", "hay hơn", "nên chọn",
            "giống nhau", "khác biệt", "phân biệt"
        }
    };

    public Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        var result = Detect(message);
        return Task.FromResult(result);
    }

    private SubIntentResult? Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var normalized = message.ToLowerInvariant().Trim();
        var categoryScores = new Dictionary<SubIntentCategory, (int MatchCount, List<string> Matched)>();

        // Match keywords for each category
        foreach (var (category, keywords) in Keywords)
        {
            var matched = new List<string>();
            foreach (var keyword in keywords)
            {
                if (normalized.Contains(keyword))
                {
                    matched.Add(keyword);
                }
            }

            if (matched.Count > 0)
            {
                categoryScores[category] = (matched.Count, matched);
            }
        }

        // No matches
        if (categoryScores.Count == 0)
            return null;

        // Find highest scoring category
        var best = categoryScores.OrderByDescending(kvp => kvp.Value.MatchCount).First();
        var confidence = CalculateConfidence(best.Value.MatchCount);

        return SubIntentResult.Create(
            category: best.Key,
            confidence: confidence,
            matchedKeywords: best.Value.Matched.ToArray(),
            explanation: $"Matched {best.Value.MatchCount} keywords",
            source: "keyword");
    }

    private static decimal CalculateConfidence(int matchCount)
    {
        return matchCount switch
        {
            >= 3 => 0.95m,  // Very high confidence
            2 => 0.85m,     // High confidence
            1 => 0.65m,     // Medium confidence
            _ => 0.0m
        };
    }
}
