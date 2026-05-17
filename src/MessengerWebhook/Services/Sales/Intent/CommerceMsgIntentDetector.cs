using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;

namespace MessengerWebhook.Services.Sales.Intent;

/// <summary>
/// Default implementation of <see cref="ICommerceMsgIntentDetector"/>.
/// Consolidates all keyword-based boolean checks that were previously scattered
/// across HandleSalesConversationAsync in SalesStateHandlerBase.
/// </summary>
public class CommerceMsgIntentDetector : ICommerceMsgIntentDetector
{
    private readonly IContactConfirmationFlow _contactFlow;
    private readonly ISalesContextResolver _contextResolver;

    public CommerceMsgIntentDetector(
        IContactConfirmationFlow contactFlow,
        ISalesContextResolver contextResolver)
    {
        _contactFlow = contactFlow;
        _contextResolver = contextResolver;
    }

    /// <inheritdoc />
    public CommerceMsgIntent DetectFromKeywords(
        string message,
        StateContext ctx,
        bool hasProduct,
        bool hasContact)
    {
        var hasBuySignal = SalesMessageParser.ContainsAnyPhrase(message,
            "lên đơn", "len don", "chốt đơn", "chot don", "chốt nhé", "chot nhe", "chốt nha", "chot nha",
            "mua luôn", "mua luon", "đặt hàng", "dat hang", "ok em", "oke em", "ok e",
            "lấy sản phẩm này", "lay san pham nay", "lấy nhé", "lay nhe", "lấy nha", "lay nha");

        var isProductQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "nói thêm", "noi them", "nói kỹ", "noi ky", "chi tiet", "thành phần", "thanh phan",
            "công dụng", "cong dung", "cách dùng", "cach dung", "phù hợp", "phu hop",
            "dùng sao", "dung sao");

        var isShippingQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "freeship", "free ship", "phí ship", "phi ship", "vận chuyển", "van chuyen", "ship");

        var isPolicyQuestion = isShippingQuestion || SalesMessageParser.ContainsAnyPhrase(message,
            "quà gì", "qua gi", "quà tặng", "qua tang", "tặng gì", "tang gi",
            "khuyến mãi", "khuyen mai", "ưu đãi", "uu dai", "giảm giá", "giam gia", "promo");

        var isPriceQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "giá bao nhiêu", "gia bao nhieu", "giá sao", "gia sao",
            "bao nhiêu tiền", "bao nhieu tien", "giá", "gia");

        var isInventoryQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "còn hàng", "con hang", "hết hàng", "het hang", "còn không", "con khong",
            "hết chưa", "het chua", "sẵn hàng", "san hang", "có sẵn", "co san",
            "out stock", "in stock", "tồn kho", "ton kho");

        var isOrderEstimateQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "tổng tiền", "tong tien", "tổng cộng", "tong cong",
            "bao nhiêu sản phẩm", "bao nhieu san pham", "bao nhiêu món", "bao nhieu mon");

        var isAmbiguousProductReference = SalesMessageParser.HasAmbiguousProductReference(message);
        var hasQuestionMarker = message.Contains('?');
        var hasProductCategoryReference = SalesMessageParser.HasProductCategoryReference(message);

        var isContactMemoryQuestion = _contactFlow.IsContactMemoryQuestion(message);
        var isPendingContactClarification = _contactFlow.IsPendingClarificationQuestion(ctx, message);
        var isGenericBuyContinuation = _contactFlow.IsGenericBuyContinuationPendingConfirmation(ctx, message);

        var isRelatedSuggestionSelection = _contextResolver.IsRelatedSuggestionSelection(message);

        // Grounding check uses isQuestioning=false here; caller will re-derive after AI merge if needed.
        // For the keyword pass we conservatively pass isQuestioning=false since AI intent is not yet known.
        var requiresProductGrounding = SalesMessageParser.RequiresProductGrounding(
            message,
            isProductQuestion,
            isPriceQuestion,
            isInventoryQuestion,
            isPolicyQuestion,
            isQuestioning: false,
            hasQuestionMarker,
            ctx.CurrentState);

        return new CommerceMsgIntent
        {
            HasBuySignal = hasBuySignal,
            HasProductQuestion = isProductQuestion,
            HasShippingQuestion = isShippingQuestion,
            HasPolicyQuestion = isPolicyQuestion,
            HasPriceQuestion = isPriceQuestion,
            HasInventoryQuestion = isInventoryQuestion,
            HasOrderEstimateQuestion = isOrderEstimateQuestion,
            HasAmbiguousProductReference = isAmbiguousProductReference,
            HasQuestionMarker = hasQuestionMarker,
            HasProductCategoryReference = hasProductCategoryReference,
            IsContactMemoryQuestion = isContactMemoryQuestion,
            IsPendingContactClarification = isPendingContactClarification,
            IsGenericBuyContinuation = isGenericBuyContinuation,
            IsRelatedSuggestionSelection = isRelatedSuggestionSelection,
            RequiresProductGrounding = requiresProductGrounding,
        };
    }

    /// <inheritdoc />
    public Task<CommerceMsgIntent> MergeWithAiIntentAsync(
        CommerceMsgIntent keywords,
        IntentDetectionResult aiIntent,
        SubIntentResult? subIntent,
        float confidenceThreshold)
    {
        var useAiIntent = aiIntent.Confidence >= confidenceThreshold;
        var isQuestioning = useAiIntent && aiIntent.Intent == CustomerIntent.Questioning;

        // Grounding was computed conservatively (isQuestioning=false) in the keyword pass.
        // Upgrade it now that we know whether AI classified the message as Questioning.
        // The flag-based overload: isPriceQuestion/isInventory/isProductQuestion already cover most paths;
        // the Questioning+hasQuestionMarker+HasProductCategoryReference path is the only delta here.
        // We carry all flags from the keyword snapshot so the result is consistent.
        var requiresProductGrounding = keywords.RequiresProductGrounding
            || (isQuestioning
                && !keywords.HasPolicyQuestion
                && keywords.HasProductCategoryReference
                && (keywords.HasQuestionMarker || keywords.HasProductQuestion || keywords.HasPriceQuestion || keywords.HasInventoryQuestion));

        var merged = keywords with
        {
            Intent = aiIntent.Intent,
            SubIntentCategory = subIntent?.Category,
            Confidence = (float)aiIntent.Confidence,
            UseAiIntent = useAiIntent,
            RequiresProductGrounding = requiresProductGrounding,
        };

        return Task.FromResult(merged);
    }
}
