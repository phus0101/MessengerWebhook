using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Reply;

/// <summary>
/// Coordinates the sales reply pipeline extracted from SalesStateHandlerBase (R-04).
/// Encapsulates A/B variant routing, the naturalness pipeline (Emotion → Tone → SmallTalk →
/// Gemini → Validation), product grounding, RAG retrieval, fallback handling, and metric logging.
///
/// Pure orchestration — owns no state. Mutates the supplied StateContext for downstream stages
/// (toneProfile, emotionScore, conversationContext, smallTalkResponse, abTestVariant,
/// vipGreetingSent) — the same surface SalesStateHandlerBase exposed before extraction.
/// </summary>
public interface ISalesReplyOrchestrator
{
    /// <summary>
    /// Generates the bot reply for a sales conversation turn.
    /// May short-circuit on small-talk paths or grounding fallbacks.
    /// </summary>
    Task<string> GenerateAsync(SalesReplyRequest request);

    /// <summary>
    /// Builds a grounded "did you mean..." reply (or fallback) when the requested product isn't allowed.
    /// Used independently by the offer/fallback flow inside SalesStateHandlerBase before the main pipeline.
    /// </summary>
    Task<string> BuildGroundedFallbackAsync(StateContext ctx, string message, GroundedProductContext groundingContext);
}
