using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.SubIntent;

namespace MessengerWebhook.Services.Sales.Intent;

/// <summary>
/// Unified snapshot of all intent signals for a single incoming message.
/// Replaces ~14 scattered boolean declarations in HandleSalesConversationAsync.
/// Keyword signals are detected synchronously; AI signals are merged in via MergeWithAiIntentAsync.
/// </summary>
public record CommerceMsgIntent
{
    // ── AI-detected signals (merged after keyword pass) ──────────────────────

    /// <summary>AI-detected customer intent category.</summary>
    public CustomerIntent Intent { get; init; } = CustomerIntent.Consulting;

    /// <summary>AI-detected sub-intent category (only set when Intent == Consulting).</summary>
    public SubIntentCategory? SubIntentCategory { get; init; }

    /// <summary>AI confidence score 0.0–1.0.</summary>
    public float Confidence { get; init; }

    /// <summary>True when AI confidence meets the configured threshold.</summary>
    public bool UseAiIntent { get; init; }

    // ── Commerce keyword signals ─────────────────────────────────────────────

    /// <summary>Message contains an explicit buy-intent phrase ("lên đơn", "chốt đơn", "ok em", etc.).</summary>
    public bool HasBuySignal { get; init; }

    /// <summary>Message asks about product details ("nói thêm", "thành phần", "cách dùng", etc.).</summary>
    public bool HasProductQuestion { get; init; }

    /// <summary>Message asks about shipping ("freeship", "phí ship", "vận chuyển", etc.).</summary>
    public bool HasShippingQuestion { get; init; }

    /// <summary>Message asks about policy/promotions (superset of HasShippingQuestion plus "khuyến mãi", "quà gì", etc.).</summary>
    public bool HasPolicyQuestion { get; init; }

    /// <summary>Message asks about pricing ("giá bao nhiêu", "bao nhiêu tiền", etc.).</summary>
    public bool HasPriceQuestion { get; init; }

    /// <summary>Message asks about stock availability ("còn hàng", "hết hàng", "còn không", etc.).</summary>
    public bool HasInventoryQuestion { get; init; }

    /// <summary>Message asks for a running order total ("tổng tiền", "tổng cộng", etc.).</summary>
    public bool HasOrderEstimateQuestion { get; init; }

    /// <summary>Message contains an ambiguous product reference ("cái này", "loại đó", etc.).</summary>
    public bool HasAmbiguousProductReference { get; init; }

    /// <summary>Message contains the '?' character.</summary>
    public bool HasQuestionMarker { get; init; }

    /// <summary>Message references a product category ("sản phẩm", "kem", "serum", etc.).</summary>
    public bool HasProductCategoryReference { get; init; }

    // ── ContactFlow signals ───────────────────────────────────────────────────

    /// <summary>Customer is asking whether their contact info is on file.</summary>
    public bool IsContactMemoryQuestion { get; init; }

    /// <summary>Contact confirmation is pending and the message is a clarification question.</summary>
    public bool IsPendingContactClarification { get; init; }

    /// <summary>Contact confirmation is pending and the message is a generic buy-continuation (not explicit confirmation).</summary>
    public bool IsGenericBuyContinuation { get; init; }

    // ── ContextResolver signals ───────────────────────────────────────────────

    /// <summary>Message is selecting one of the numbered related-product suggestions shown earlier.</summary>
    public bool IsRelatedSuggestionSelection { get; init; }

    // ── Derived signals ───────────────────────────────────────────────────────

    /// <summary>Product grounding (RAG lookup) is required to answer this message.</summary>
    public bool RequiresProductGrounding { get; init; }

    /// <summary>Convenience: AI intent is Questioning.</summary>
    public bool IsQuestioning => UseAiIntent && Intent == CustomerIntent.Questioning;
}
