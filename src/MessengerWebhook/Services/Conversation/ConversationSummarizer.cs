using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;

namespace MessengerWebhook.Services.Conversation;

/// <summary>
/// Summarizes older conversation turns into a compact context block using FlashLite.
/// Called by ConversationHistoryHelper when history exceeds SummarizationThreshold.
/// Output is stored in ctx.Data["conversationSummary"] and injected into the system prompt.
/// </summary>
public class ConversationSummarizer : IConversationSummarizer
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<ConversationSummarizer> _logger;

    public ConversationSummarizer(
        IGeminiService geminiService,
        ILogger<ConversationSummarizer> logger)
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    public async Task<string> SummarizeAsync(
        IReadOnlyList<ConversationMessage> olderTurns,
        string? existingSummary,
        CancellationToken ct = default)
    {
        if (olderTurns.Count == 0)
            return existingSummary ?? string.Empty;

        var turnLines = olderTurns.Select(t =>
        {
            var speaker = t.Role == "assistant" ? "Bot" : "Khách";
            return $"{speaker}: {t.Content}";
        });

        var conversationText = string.Join("\n", turnLines);

        var existingBlock = string.IsNullOrWhiteSpace(existingSummary)
            ? string.Empty
            : $"\n\nTóm tắt trước đó:\n{existingSummary}";

        var prompt = $@"Bạn là trợ lý tóm tắt hội thoại bán hàng. Hãy tóm tắt ngắn gọn các lượt hội thoại sau thành định dạng chuẩn.{existingBlock}

Hội thoại cần tóm tắt:
{conversationText}

Hãy xuất kết quả theo đúng định dạng sau (không thêm gì khác):
Sản phẩm quan tâm: [mã sản phẩm hoặc tên, hoặc ""chưa rõ""]
Liên hệ đã thu: SĐT=[số điện thoại hoặc ""chưa có""], Địa chỉ=[địa chỉ hoặc ""chưa có""]
Intent khách: [mô tả ngắn gọn mục đích khách hàng]
Điểm đặc biệt: [các điểm quan trọng cần nhớ, hoặc ""không có""]";

        try
        {
            // Use FlashLite for cost efficiency; empty history = one-off prompt
            var summary = await _geminiService.SendMessageAsync(
                userId: "summarizer",
                message: prompt,
                history: new List<ConversationMessage>(),
                modelOverride: GeminiModelType.FlashLite,
                cancellationToken: ct);

            _logger.LogInformation(
                "ConversationSummarized TurnsProcessed={Count} SummaryLength={Length}",
                olderTurns.Count, summary.Length);

            return summary.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize conversation turns. Returning existing summary.");
            // Graceful degradation: keep existing summary rather than losing context
            return existingSummary ?? string.Empty;
        }
    }
}
