using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Models;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.AI.Routing;

/// <summary>
/// Routes LLM calls to the appropriate model tier based on conversation context.
/// Decision matrix (evaluated in priority order):
///   1. Purpose "classify" or "summarize" → FlashLite (cost efficiency)
///   2. VIP customer, high ticket value, low AI confidence, or long history → Pro
///   3. Default → Flash (standard chat)
/// </summary>
public class LlmRoutingService : ILlmRoutingService
{
    private readonly LlmRoutingOptions _options;
    private readonly ILogger<LlmRoutingService> _logger;

    public LlmRoutingService(
        IOptions<LlmRoutingOptions> options,
        ILogger<LlmRoutingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public GeminiModelType SelectModel(LlmRoutingContext ctx)
    {
        if (!_options.Enabled)
        {
            // When routing is disabled, fall back to Flash for chat and FlashLite otherwise
            return ctx.Purpose is "classify" or "summarize"
                ? GeminiModelType.FlashLite
                : GeminiModelType.Flash;
        }

        // Lowest-cost tier for non-generative tasks
        if (ctx.Purpose is "classify" or "summarize")
        {
            _logger.LogDebug("LlmRouting: FlashLite selected — Purpose={Purpose}", ctx.Purpose);
            return GeminiModelType.FlashLite;
        }

        // Pro tier triggers
        var ticketValue = ctx.EstimatedTicketValue ?? 0m;
        var intentConfidence = ctx.Intent?.Confidence ?? 1f;

        if (ctx.IsVipCustomer)
        {
            _logger.LogDebug("LlmRouting: Pro selected — VIP customer");
            return GeminiModelType.Pro;
        }

        if (ticketValue >= _options.ProTierMinTicketValueVnd)
        {
            _logger.LogDebug("LlmRouting: Pro selected — TicketValue={Value}", ticketValue);
            return GeminiModelType.Pro;
        }

        if (intentConfidence < _options.LowConfidenceThreshold)
        {
            _logger.LogDebug("LlmRouting: Pro selected — LowConfidence={Confidence}", intentConfidence);
            return GeminiModelType.Pro;
        }

        if (ctx.HistoryTurnCount > _options.LongConversationThreshold)
        {
            _logger.LogDebug("LlmRouting: Pro selected — LongHistory={Turns}", ctx.HistoryTurnCount);
            return GeminiModelType.Pro;
        }

        _logger.LogDebug("LlmRouting: Flash selected — standard chat");
        return GeminiModelType.Flash;
    }
}
