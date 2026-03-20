using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.AI.Strategies;

public class HybridModelSelectionStrategy : IModelSelectionStrategy
{
    private readonly string[] _complexKeywords =
    {
        "tư vấn", "gợi ý", "phù hợp", "nên mặc", "đề xuất",
        "recommend", "suggest", "advice", "giúp tôi chọn",
        "so sánh", "khác nhau", "compare"
    };

    public GeminiModelType SelectModel(string message)
    {
        // Use Pro for complex consultation
        if (message.Length > 100 ||
            _complexKeywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return GeminiModelType.Pro;
        }

        // Use Flash-Lite for simple queries
        return GeminiModelType.FlashLite;
    }
}
