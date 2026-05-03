using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;
using Microsoft.Extensions.Options;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.StateMachine.Handlers;

public abstract class SalesStateHandlerBase : IStateHandler
{
    protected readonly IGeminiService GeminiService;
    protected readonly IPolicyGuardService PolicyGuardService;
    protected readonly IProductMappingService ProductMappingService;
    protected readonly IGiftSelectionService GiftSelectionService;
    protected readonly IFreeshipCalculator FreeshipCalculator;
    protected readonly ICaseEscalationService CaseEscalationService;
    protected readonly ICustomerIntelligenceService CustomerIntelligenceService;
    protected readonly DraftOrderCoordinator DraftOrderCoordinator;
    protected readonly IRAGService? RagService;
    protected readonly IEmotionDetectionService EmotionDetectionService;
    protected readonly IToneMatchingService ToneMatchingService;
    protected readonly IConversationContextAnalyzer ConversationContextAnalyzer;
    protected readonly ISmallTalkService SmallTalkService;
    protected readonly IResponseValidationService ResponseValidationService;
    protected readonly IABTestService ABTestService;
    protected readonly IConversationMetricsService ConversationMetricsService;
    protected readonly ISubIntentClassifier SubIntentClassifier;
    private readonly IProductGroundingService _productGroundingService;
    protected readonly SalesBotOptions SalesBotOptions;
    protected readonly PolicyGuardOptions PolicyGuardOptions;
    protected readonly RAGOptions RagOptions;
    protected readonly ILogger Logger;

    public abstract ConversationState HandledState { get; }

    protected SalesStateHandlerBase(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        ICustomerIntelligenceService customerIntelligenceService,
        DraftOrderCoordinator draftOrderCoordinator,
        IRAGService? ragService,
        IEmotionDetectionService emotionDetectionService,
        IToneMatchingService toneMatchingService,
        IConversationContextAnalyzer conversationContextAnalyzer,
        ISmallTalkService smallTalkService,
        IResponseValidationService responseValidationService,
        IABTestService abTestService,
        IConversationMetricsService conversationMetricsService,
        ISubIntentClassifier subIntentClassifier,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger logger,
        IProductGroundingService? productGroundingService = null)
        : this(
            geminiService,
            policyGuardService,
            productMappingService,
            giftSelectionService,
            freeshipCalculator,
            caseEscalationService,
            customerIntelligenceService,
            draftOrderCoordinator,
            ragService,
            emotionDetectionService,
            toneMatchingService,
            conversationContextAnalyzer,
            smallTalkService,
            responseValidationService,
            abTestService,
            conversationMetricsService,
            subIntentClassifier,
            salesBotOptions,
            Options.Create(new PolicyGuardOptions()),
            ragOptions,
            logger,
            productGroundingService)
    {
    }

    protected SalesStateHandlerBase(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        ICustomerIntelligenceService customerIntelligenceService,
        DraftOrderCoordinator draftOrderCoordinator,
        IRAGService? ragService,
        IEmotionDetectionService emotionDetectionService,
        IToneMatchingService toneMatchingService,
        IConversationContextAnalyzer conversationContextAnalyzer,
        ISmallTalkService smallTalkService,
        IResponseValidationService responseValidationService,
        IABTestService abTestService,
        IConversationMetricsService conversationMetricsService,
        ISubIntentClassifier subIntentClassifier,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<PolicyGuardOptions> policyGuardOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger logger,
        IProductGroundingService? productGroundingService = null)
    {
        GeminiService = geminiService;
        PolicyGuardService = policyGuardService;
        ProductMappingService = productMappingService;
        GiftSelectionService = giftSelectionService;
        FreeshipCalculator = freeshipCalculator;
        CaseEscalationService = caseEscalationService;
        CustomerIntelligenceService = customerIntelligenceService;
        DraftOrderCoordinator = draftOrderCoordinator;
        RagService = ragService;
        EmotionDetectionService = emotionDetectionService;
        ToneMatchingService = toneMatchingService;
        ConversationContextAnalyzer = conversationContextAnalyzer;
        SmallTalkService = smallTalkService;
        ResponseValidationService = responseValidationService;
        ABTestService = abTestService;
        ConversationMetricsService = conversationMetricsService;
        SubIntentClassifier = subIntentClassifier;
        _productGroundingService = productGroundingService ?? new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector());
        SalesBotOptions = salesBotOptions.Value;
        PolicyGuardOptions = policyGuardOptions.Value;
        RagOptions = ragOptions.Value;
        Logger = logger;
    }

    public async Task<string> HandleAsync(StateContext ctx, string message)
    {
        try
        {
            Logger.LogInformation("Handling state {State} for PSID: {PSID}", HandledState, ctx.FacebookPSID);
            return await HandleInternalAsync(ctx, message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sales state error in {State} for PSID {PSID}", HandledState, ctx.FacebookPSID);
            ctx.CurrentState = ConversationState.Error;
            return "Dạ em đang bị nghẽn ở hệ thống một chút. Chị nhắn lại giúp em sau ít phút nha.";
        }
    }

    protected abstract Task<string> HandleInternalAsync(StateContext ctx, string message);

    protected async Task<string> HandleSalesConversationAsync(StateContext ctx, string message)
    {
        AddToHistory(ctx, "user", message);

        // Load remembered contact from previous orders on first message
        var history = GetHistory(ctx);
        Logger.LogInformation("History count: {Count} for PSID: {PSID}", history.Count, ctx.FacebookPSID);

        if (history.Count <= 1) // First message in conversation
        {
            var pageId = ctx.GetData<string>("facebookPageId");
            Logger.LogInformation("Attempting to load customer for PSID: {PSID}, PageId: {PageId}", ctx.FacebookPSID, pageId);

            var customer = await CustomerIntelligenceService.GetExistingAsync(
                ctx.FacebookPSID,
                pageId);

            Logger.LogInformation("Customer lookup result - Found: {Found}, TotalOrders: {Orders}, Phone: {HasPhone}, Address: {HasAddress}",
                customer != null,
                customer?.TotalOrders ?? 0,
                !string.IsNullOrWhiteSpace(customer?.PhoneNumber),
                !string.IsNullOrWhiteSpace(customer?.ShippingAddress));

            if (customer is { TotalOrders: > 0 })
            {
                // Mark as returning customer (used for greeting/tone adaptation)
                ctx.SetData("isReturningCustomer", true);
                ctx.SetData("totalOrders", customer.TotalOrders);

                if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
                {
                    ctx.SetData("rememberedCustomerPhone", customer.PhoneNumber);
                    ctx.SetData("customerPhone", customer.PhoneNumber);
                    Logger.LogInformation("Loaded remembered phone for PSID: {PSID}: {Phone}", ctx.FacebookPSID, PiiRedaction.MaskPhone(customer.PhoneNumber ?? string.Empty));
                }

                if (!string.IsNullOrWhiteSpace(customer.ShippingAddress))
                {
                    ctx.SetData("rememberedShippingAddress", customer.ShippingAddress);
                    ctx.SetData("shippingAddress", customer.ShippingAddress);
                    Logger.LogInformation("Loaded remembered address for PSID: {PSID}: {Address}", ctx.FacebookPSID, PiiRedaction.MaskAddress(customer.ShippingAddress ?? string.Empty));
                }

                // Set flag to ask for confirmation
                if (!string.IsNullOrWhiteSpace(customer.PhoneNumber) || !string.IsNullOrWhiteSpace(customer.ShippingAddress))
                {
                    ctx.SetData("contactNeedsConfirmation", true);
                    ctx.SetData("contactMemorySource", "previous-order");
                    ctx.SetData("pendingContactQuestion", "confirm_old_contact");
                    Logger.LogInformation("Set contactNeedsConfirmation=true for PSID: {PSID}", ctx.FacebookPSID);
                }
            }
        }

        var policyRequest = BuildPolicyGuardRequest(ctx, message, history);
        var decision = await PolicyGuardService.EvaluateAsync(policyRequest);
        if (decision.Action == PolicyAction.SafeReply)
        {
            var safeReply = PolicyGuardOptions.SafeReplyMessage;
            AddToHistory(ctx, "assistant", safeReply);
            return safeReply;
        }

        if (decision.RequiresEscalation)
        {
            var supportCase = await CaseEscalationService.EscalateAsync(
                ctx.FacebookPSID,
                decision.Reason,
                decision.Summary,
                message);

            ctx.SetData("supportCaseId", supportCase.Id);
            ctx.CurrentState = ConversationState.HumanHandoff;
            var handoffResponse = SalesBotOptions.UnsupportedFallbackMessage;
            AddToHistory(ctx, "assistant", handoffResponse);
            return handoffResponse;
        }

        // Capture customer details first (phone, address, etc.)
        await SalesMessageParser.CaptureCustomerDetailsAsync(ctx, message, GeminiService, Logger);
        SalesMessageParser.CaptureSelectedProductQuantity(ctx, message);

        Logger.LogInformation(
            "After CaptureCustomerDetails - PSID: {PSID}, HasProduct: {HasProduct}, HasRequiredContact: {HasRequiredContact}, NeedsConfirmation: {NeedsConfirmation}",
            ctx.FacebookPSID,
            HasSelectedProduct(ctx),
            SalesMessageParser.HasRequiredContact(ctx),
            ctx.GetData<bool?>("contactNeedsConfirmation") ?? false
        );

        if (history.Count <= 1 && IsPureGreeting(message) && !HasSelectedProduct(ctx))
        {
            var greetingReply = await BuildFirstGreetingReplyAsync(ctx);
            ctx.CurrentState = ConversationState.Consulting;
            ctx.SetData("vipGreetingSent", true);
            AddToHistory(ctx, "assistant", greetingReply);
            return greetingReply;
        }

        // AI Intent Detection - understand customer's true intent BEFORE building offer
        var hasProduct = HasSelectedProduct(ctx);
        var hasContact = SalesMessageParser.HasRequiredContact(ctx);
        var hasBuyIntentPhrase = ContainsAnyPhrase(message,
            "lên đơn", "len don", "chốt đơn", "chot don", "chốt nhé", "chot nhe", "chốt nha", "chot nha",
            "mua luôn", "mua luon", "đặt hàng", "dat hang", "ok em", "oke em", "ok e",
            "lấy sản phẩm này", "lay san pham nay", "lấy nhé", "lay nhe", "lấy nha", "lay nha");
        var isRelatedSuggestionSelection = IsRelatedSuggestionSelection(message);
        var hasPendingFinalSummaryConfirmation = IsAwaitingFinalSummaryConfirmation(ctx);
        var activeProductsForIntent = await GetActiveSelectedProductsAsync(ctx);
        var intentGroundingContext = _productGroundingService.BuildContext(message, activeProductsForIntent, Array.Empty<GroundedProduct>());
        var recentHistory = _productGroundingService
            .SanitizeAssistantHistory(GetHistory(ctx).TakeLast(3), intentGroundingContext.AllowedProducts)
            .ToList();
        var intentResult = await GeminiService.DetectIntentAsync(
            message,
            ctx.CurrentState,
            hasProduct,
            hasContact,
            recentHistory,
            CancellationToken.None);

        var resolvedRelatedSuggestionSelection = false;
        if (isRelatedSuggestionSelection)
        {
            resolvedRelatedSuggestionSelection = await TryResolveNumberedSuggestionSelectionAsync(ctx, message) != null;
            hasProduct = HasSelectedProduct(ctx);
        }

        Logger.LogInformation(
            "AI Intent Detection - PSID: {PSID}, Intent: {Intent}, Confidence: {Confidence}, Method: {Method}",
            ctx.FacebookPSID,
            intentResult.Intent,
            intentResult.Confidence,
            intentResult.DetectionMethod
        );

        // Track consultation rejections: if customer says ReadyToBuy after bot asked consultation question
        var lastBotMessage = recentHistory.LastOrDefault(m => m.Role == "assistant")?.Content ?? string.Empty;
        var isConsultationQuestion = new[] { "cần tư vấn", "tư vấn thêm", "hỏi thêm", "thắc mắc" }
            .Any(k => lastBotMessage.ToLower().Contains(k)) && lastBotMessage.Contains("?");

        if (isConsultationQuestion &&
            intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy &&
            intentResult.Confidence >= SalesBotOptions.IntentConfidenceThreshold)
        {
            var currentCount = ctx.GetData<int>("consultationRejectionCount");
            ctx.SetData("consultationRejectionCount", currentCount + 1);

            Logger.LogInformation(
                "Consultation rejection detected (count: {Count}) for PSID: {PSID}",
                currentCount + 1,
                ctx.FacebookPSID
            );
        }

        // Route based on AI-detected intent (only if confidence meets threshold)
        var useAiIntent = intentResult.Confidence >= SalesBotOptions.IntentConfidenceThreshold;

        // Recover product from history when customer is already in ordering flow but product context was lost.
        if (!hasProduct &&
            (hasContact ||
             hasBuyIntentPhrase ||
             (isRelatedSuggestionSelection && resolvedRelatedSuggestionSelection) ||
             ctx.CurrentState == ConversationState.CollectingInfo ||
             (useAiIntent &&
              (intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
               intentResult.Intent == Services.AI.Models.CustomerIntent.Confirming))))
        {
            Logger.LogInformation("Ordering flow detected without product in context for PSID: {PSID}, attempting to extract product from history", ctx.FacebookPSID);
            await TryExtractProductFromHistoryAsync(ctx, message);
            hasProduct = HasSelectedProduct(ctx);
        }

        if (isRelatedSuggestionSelection && hasProduct)
        {
            var selectedSuggestionReply = await TryBuildOfferResponseAsync(ctx, message, Services.AI.Models.CustomerIntent.Browsing);
            if (!string.IsNullOrWhiteSpace(selectedSuggestionReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", selectedSuggestionReply);
                return selectedSuggestionReply;
            }
        }

        var nextState = useAiIntent
            ? DetermineNextState(intentResult.Intent, hasProduct, hasContact)
            : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        var isQuestioning = useAiIntent && intentResult.Intent == Services.AI.Models.CustomerIntent.Questioning;
        var isProductQuestion = ContainsAnyPhrase(message,
            "nói thêm", "noi them", "nói kỹ", "noi ky", "chi tiet", "thành phần", "thanh phan", "công dụng", "cong dung",
            "cách dùng", "cach dung", "phù hợp", "phu hop", "dùng sao", "dung sao");
        var isShippingQuestion = ContainsAnyPhrase(message,
            "freeship", "free ship", "phí ship", "phi ship", "vận chuyển", "van chuyen", "ship");
        var isPolicyQuestion = isShippingQuestion || ContainsAnyPhrase(message,
            "quà gì", "qua gi", "quà tặng", "qua tang", "tặng gì", "tang gi",
            "khuyến mãi", "khuyen mai", "ưu đãi", "uu dai", "giảm giá", "giam gia", "promo");
        var isPriceQuestion = ContainsAnyPhrase(message,
            "giá bao nhiêu", "gia bao nhieu", "giá sao", "gia sao", "bao nhiêu tiền", "bao nhieu tien", "giá", "gia");
        var isInventoryQuestion = ContainsAnyPhrase(message,
            "còn hàng", "con hang", "hết hàng", "het hang", "còn không", "con khong", "hết chưa", "het chua",
            "sẵn hàng", "san hang", "có sẵn", "co san", "out stock", "in stock", "tồn kho", "ton kho");
        var isContactMemoryQuestion = ContainsAnyPhrase(message,
            "có thông tin của chị chưa", "co thong tin cua chi chua", "em có thông tin của chị chưa", "em co thong tin cua chi chua",
            "có số của chị chưa", "co so cua chi chua", "có địa chỉ của chị chưa", "co dia chi cua chi chua",
            "em có số điện thoại của chị chưa", "em co so dien thoai cua chi chua");
        var isPendingContactClarification = needsConfirmation &&
                                           string.Equals(ctx.GetData<string>("pendingContactQuestion"), "confirm_old_contact", StringComparison.OrdinalIgnoreCase) &&
                                           ContainsAnyPhrase(message, "thông tin nào", "thong tin nao", "thông tin gì", "thong tin gi", "xác nhận thông tin nào", "xac nhan thong tin nao");
        var isGenericPendingContactBuyReply = needsConfirmation &&
                                              string.Equals(ctx.GetData<string>("pendingContactQuestion"), "confirm_old_contact", StringComparison.OrdinalIgnoreCase) &&
                                              IsGenericBuyContinuationWhileAwaitingContactConfirmation(message);
        var isOrderEstimateQuestion = ContainsAnyPhrase(message,
            "tổng tiền", "tong tien", "tổng cộng", "tong cong", "bao nhiêu sản phẩm", "bao nhieu san pham", "bao nhiêu món", "bao nhieu mon");
        var isAmbiguousProductReference = HasAmbiguousProductReference(message);
        var hasQuestionMarker = message.Contains('?');
        var requiresProductGrounding = RequiresProductGrounding(
            message,
            isProductQuestion,
            isPriceQuestion,
            isInventoryQuestion,
            isPolicyQuestion,
            isQuestioning,
            hasQuestionMarker,
            ctx.CurrentState);

        if (hasPendingFinalSummaryConfirmation)
        {
            var finalSummaryReply = await HandlePendingFinalSummaryConfirmationAsync(ctx, message, useAiIntent ? intentResult.Intent : null);
            if (!string.IsNullOrWhiteSpace(finalSummaryReply))
            {
                AddToHistory(ctx, "assistant", finalSummaryReply);
                return finalSummaryReply;
            }
        }

        if (isAmbiguousProductReference)
        {
            var ambiguousReferenceReply = await BuildAmbiguousProductClarificationReplyAsync(ctx);
            if (!string.IsNullOrWhiteSpace(ambiguousReferenceReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", ambiguousReferenceReply);
                return ambiguousReferenceReply;
            }
        }

        if (isContactMemoryQuestion)
        {
            var contactMemoryReply = await BuildContactMemoryReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactMemoryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactMemoryReply);
                return contactMemoryReply;
            }
        }

        if (isPendingContactClarification)
        {
            var contactClarificationReply = BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactClarificationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactClarificationReply);
                return contactClarificationReply;
            }
        }

        if (isGenericPendingContactBuyReply)
        {
            var contactConfirmationReply = BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactConfirmationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactConfirmationReply);
                return contactConfirmationReply;
            }
        }

        if (isOrderEstimateQuestion)
        {
            var orderEstimateReply = await BuildOrderEstimateReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(orderEstimateReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", orderEstimateReply);
                return orderEstimateReply;
            }
        }

        if (isProductQuestion || (isQuestioning && !isPolicyQuestion && !isPriceQuestion && !isInventoryQuestion && !isContactMemoryQuestion && !isPendingContactClarification && !isOrderEstimateQuestion && (hasQuestionMarker || ctx.CurrentState == ConversationState.Consulting)))
        {
            var consultReply = await BuildProductConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(consultReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", consultReply);
                return consultReply;
            }
        }

        if (isPolicyQuestion || (isQuestioning && hasQuestionMarker && hasBuyIntentPhrase == false && !isPriceQuestion && !isInventoryQuestion && !isOrderEstimateQuestion))
        {
            var shippingReply = await BuildShippingConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(shippingReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", shippingReply);
                return shippingReply;
            }
        }

        if (isInventoryQuestion)
        {
            var inventoryReply = await BuildInventoryConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(inventoryReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", inventoryReply);
                return inventoryReply;
            }

            var fallbackReply = BuildProductGroundingFallbackReply();
            ctx.CurrentState = ConversationState.Consulting;
            AddToHistory(ctx, "assistant", fallbackReply);
            return fallbackReply;
        }

        if (isPriceQuestion)
        {
            var priceReply = await BuildPriceConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(priceReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                AddToHistory(ctx, "assistant", priceReply);
                return priceReply;
            }

            var fallbackReply = BuildProductGroundingFallbackReply();
            ctx.CurrentState = ConversationState.Consulting;
            AddToHistory(ctx, "assistant", fallbackReply);
            return fallbackReply;
        }

        if (hasBuyIntentPhrase && hasProduct)
        {
            var contactReply = await BuildContactCollectionReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactReply);
                return contactReply;
            }
        }

        // Only create draft order if:
        // 1. Has both contact and product
        // 2. Contact info is confirmed (not waiting for confirmation)
        // 3. Customer is explicitly in buy/confirm path, not just asking a question
        var canCreateDraftNow = hasContact &&
                                hasProduct &&
                                !needsConfirmation &&
                                (
                                    (useAiIntent &&
                                     (intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                                      intentResult.Intent == Services.AI.Models.CustomerIntent.Confirming)) ||
                                    (ctx.CurrentState == ConversationState.CollectingInfo &&
                                     (!useAiIntent ||
                                      (intentResult.Intent != Services.AI.Models.CustomerIntent.Questioning &&
                                       intentResult.Intent != Services.AI.Models.CustomerIntent.Consulting)))
                                );

        if (canCreateDraftNow)
        {
            Logger.LogInformation(
                "Customer has all required info and explicit order intent for PSID: {PSID}, building final confirmation summary",
                ctx.FacebookPSID);

            var finalSummaryReply = await BuildFinalOrderConfirmationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(finalSummaryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", finalSummaryReply);
                return finalSummaryReply;
            }
        }

        // Auto-close after repeated consultation rejections
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        if (rejectionCount >= SalesBotOptions.MaxConsultationAttempts &&
            useAiIntent &&
            intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy &&
            hasProduct)
        {
            Logger.LogInformation(
                "Auto-closing after {Count} consultation rejections - PSID: {PSID}",
                rejectionCount, ctx.FacebookPSID);

            ctx.SetData("consultationDeclined", true);

            // If has contact info, create order immediately (only if confirmed)
            if (hasContact && !needsConfirmation)
            {
                // Extract product from history if not already in context
                if (!hasProduct)
                {
                    Logger.LogInformation("No product in context before creating draft order for PSID: {PSID}, attempting to extract from history", ctx.FacebookPSID);
                    await TryExtractProductFromHistoryAsync(ctx, message);
                    hasProduct = HasSelectedProduct(ctx);
                }

                if (!hasProduct)
                {
                    Logger.LogWarning("Cannot create draft order without product for PSID: {PSID}", ctx.FacebookPSID);
                    var noProductReply = "Dạ em chưa rõ chị muốn đặt sản phẩm nào ạ. Chị cho em biết tên sản phẩm để em lên đơn nhé.";
                    AddToHistory(ctx, "assistant", noProductReply);
                    return noProductReply;
                }

                var finalSummaryReply = await BuildFinalOrderConfirmationReplyAsync(ctx, message);
                if (!string.IsNullOrWhiteSpace(finalSummaryReply))
                {
                    ctx.CurrentState = ConversationState.CollectingInfo;
                    AddToHistory(ctx, "assistant", finalSummaryReply);
                    return finalSummaryReply;
                }
            }

            // Otherwise, move to collecting info
            ctx.CurrentState = ConversationState.CollectingInfo;
            var missingInfo = GetMissingContactInfo(ctx);
            var missing = string.Join(" và ", missingInfo);
            var autoCloseReply = $"Vậy là mình chốt đơn này luôn nha chị. Chị cho em xin {missing} để em lên đơn ạ.";
            AddToHistory(ctx, "assistant", autoCloseReply);
            return autoCloseReply;
        }

        // Build product offer only when customer is browsing or explicitly wants to buy.
        string? offerResponse = null;
        if (useAiIntent && (intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                            intentResult.Intent == Services.AI.Models.CustomerIntent.Browsing))
        {
            offerResponse = await TryBuildOfferResponseAsync(ctx, message, intentResult.Intent);
        }

        // Show product offer if available and intent allows it
        if (!string.IsNullOrWhiteSpace(offerResponse))
        {
            ctx.CurrentState = nextState;
            AddToHistory(ctx, "assistant", offerResponse);
            return offerResponse;
        }

        if (requiresProductGrounding && (!RagOptions.Enabled || RagService == null))
        {
            var activeProductsForFallback = await GetActiveSelectedProductsAsync(ctx);
            var groundingContext = await _productGroundingService.BuildContextWithRelatedSuggestionsAsync(
                message,
                activeProductsForFallback,
                Array.Empty<GroundedProduct>());

            var fallbackReply = groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts
                ? await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null)
                : BuildProductGroundingFallbackReply();

            ctx.CurrentState = ConversationState.Consulting;
            AddToHistory(ctx, "assistant", fallbackReply);
            return fallbackReply;
        }

        // Continue conversation based on detected intent
        ctx.CurrentState = nextState;
        var reply = await BuildNaturalReplyAsync(ctx, message, useAiIntent ? intentResult.Intent : null);
        AddToHistory(ctx, "assistant", reply);
        return reply;
    }

    protected string BuildHumanHandoffReply()
    {
        return SalesBotOptions.UnsupportedFallbackMessage;
    }

    protected void AddToHistory(StateContext ctx, string role, string content)
    {
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
        history.Add(new AiConversationMessage { Role = role, Content = content, Timestamp = DateTime.UtcNow });

        // Enforce conversation history limit to prevent memory leak
        var limit = SalesBotOptions.ConversationHistoryLimit;
        if (history.Count > limit)
        {
            // Keep most recent messages
            history = history.Skip(history.Count - limit).ToList();
        }

        ctx.SetData("conversationHistory", history);
    }

    protected List<AiConversationMessage> GetHistory(StateContext ctx)
    {
        return ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
    }

    private PolicyGuardRequest BuildPolicyGuardRequest(
        StateContext ctx,
        string message,
        IReadOnlyList<AiConversationMessage> history)
    {
        var selectedProductCodes = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).ToArray();
        var previousTurns = history;
        if (previousTurns.Count > 0 &&
            string.Equals(previousTurns[^1].Role, "user", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previousTurns[^1].Content, message, StringComparison.Ordinal))
        {
            previousTurns = previousTurns.Take(previousTurns.Count - 1).ToArray();
        }

        var recentTurns = previousTurns
            .TakeLast(PolicyGuardOptions.MaxRecentTurns)
            .Select(turn => new PolicyConversationTurn(turn.Role, turn.Content))
            .Append(new PolicyConversationTurn("user", message))
            .ToArray();

        return new PolicyGuardRequest(
            message,
            ctx.GetData<Guid?>("supportCaseId").HasValue,
            ctx.GetData<Guid?>("draftOrderId").HasValue || !string.IsNullOrWhiteSpace(ctx.GetData<string>("draftOrderCode")),
            recentTurns,
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"),
            ctx.CurrentState.ToString(),
            ctx.GetData<string>("knownIntent"),
            selectedProductCodes);
    }

    private async Task TryExtractProductFromHistoryAsync(StateContext ctx, string? currentMessage = null)
    {
        if (HasSelectedProduct(ctx))
        {
            Logger.LogDebug("Skip history product recovery because active product already exists for PSID: {PSID}", ctx.FacebookPSID);
            return;
        }

        Logger.LogInformation("Attempting to extract product from conversation history for PSID: {PSID}", ctx.FacebookPSID);

        var recentMessages = GetHistory(ctx).TakeLast(10).ToList();
        var hasNumberedSelection = ExtractRelatedSuggestionSelectionNumber(currentMessage).HasValue;
        if (await TryResolveNumberedSuggestionSelectionAsync(ctx, currentMessage) != null || hasNumberedSelection)
        {
            return;
        }

        var userCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "user");
        var assistantCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "assistant");

        var preferredCandidates = userCandidates.Count > 0 ? userCandidates : assistantCandidates;
        var preferredRole = userCandidates.Count > 0 ? "user" : "assistant";

        if (preferredCandidates.Count == 0)
        {
            Logger.LogWarning("Could not extract any product from conversation history for PSID: {PSID}", ctx.FacebookPSID);
            return;
        }

        var resolvedCandidate = preferredCandidates.Count == 1
            ? preferredCandidates[0]
            : await ResolveAmbiguousHistoryProductCandidateAsync(ctx, recentMessages, preferredCandidates, preferredRole)
              ?? preferredCandidates[0];

        Logger.LogInformation(
            "Extracted product {ProductName} (Code: {ProductCode}) from {Role} history for PSID: {PSID}",
            resolvedCandidate.Product.Name,
            resolvedCandidate.Product.Code,
            resolvedCandidate.Role,
            ctx.FacebookPSID);

        await ApplyResolvedProductAsync(ctx, resolvedCandidate.Product, "history-recovery");
    }

    private async Task<string?> TryBuildOfferResponseAsync(
        StateContext ctx,
        string message,
        Services.AI.Models.CustomerIntent intent)
    {
        Logger.LogInformation(
            "TryBuildOfferResponseAsync called for PSID: {PSID}, Intent: {Intent}, MessageLength={MessageLength}",
            ctx.FacebookPSID, intent, message.Length);

        var product = await ResolveCurrentProductAsync(ctx, message);
        if (product == null)
        {
            Logger.LogWarning(
                "No product found for PSID: {PSID}, returning null",
                ctx.FacebookPSID);
            return null;
        }

        await RefreshSelectedProductPolicyContextAsync(ctx, message);

        var productCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (productCodes.Count == 0)
        {
            productCodes = new List<string> { product.Code };
            ctx.SetData("selectedProductCodes", productCodes);
        }

        var giftCode = ctx.GetData<string>("selectedGiftCode");
        var giftName = ctx.GetData<string>("selectedGiftName");
        Gift? gift = null;
        if (!string.IsNullOrWhiteSpace(giftCode) || !string.IsNullOrWhiteSpace(giftName))
        {
            gift = new Gift { Code = giftCode ?? string.Empty, Name = giftName ?? string.Empty };
        }

        var snapshot = await BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
        var priceMessage = snapshot?.PriceConfirmed == true && snapshot.ConfirmedPrice.HasValue
            ? $"Dạ bên em có {product.Name}, giá {snapshot.ConfirmedPrice.Value:N0}đ ạ."
            : $"Dạ bên em có {product.Name} ạ. Giá chính xác em cần chốt theo phiên bản hiện tại rồi báo chị chuẩn hơn nha.";
        var readyToBuyPriceMessage = snapshot?.PriceConfirmed == true && snapshot.ConfirmedPrice.HasValue
            ? $"Dạ em lên thông tin cho {product.Name} rồi nha, giá {snapshot.ConfirmedPrice.Value:N0}đ ạ."
            : $"Dạ em lên thông tin cho {product.Name} rồi nha. Giá chính xác em cần chốt theo phiên bản hiện tại rồi báo chị chuẩn hơn nha.";
        var shippingMessage = "Về phí ship, em cần kiểm tra lại theo địa chỉ cụ thể của chị để báo chính xác nha.";
        var giftMessage = gift == null
            ? "Hiện tại em chưa thấy quà tặng nào được xác nhận cho sản phẩm này ạ."
            : $"Quà tặng kèm theo là {gift.Name} ạ. Nếu có ưu đãi khác, em sẽ cập nhật thêm khi chốt đơn.";

        if (intent == Services.AI.Models.CustomerIntent.Browsing)
        {
            var lines = new List<string>
            {
                priceMessage,
                string.Empty,
                shippingMessage,
                string.Empty,
                giftMessage,
                string.Empty,
                "Chị muốn em tư vấn thêm về công dụng hay cách dùng không ạ?"
            };

            return string.Join(Environment.NewLine, lines);
        }

        var readyToBuyLines = new List<string>
        {
            readyToBuyPriceMessage,
            string.Empty,
            shippingMessage,
            string.Empty,
            giftMessage,
            string.Empty,
            SalesMessageParser.BuildMissingInfoPrompt(ctx)
        };

        return string.Join(Environment.NewLine, readyToBuyLines);
    }

    private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null)
    {
        var startTime = DateTime.UtcNow;

        // A/B Test: Check variant assignment
        var variant = await ABTestService.GetVariantAsync(ctx.FacebookPSID, ctx.SessionId, CancellationToken.None);
        ctx.SetData("abTestVariant", variant);

        Logger.LogInformation(
            "A/B Test variant for PSID {PSID}: {Variant} (Enabled: {Enabled})",
            ctx.FacebookPSID,
            variant,
            ABTestService.IsEnabled());

        // Control group: Skip naturalness pipeline, use direct AI response
        if (variant == "control")
        {
            Logger.LogInformation("Control group: Skipping naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);
            var controlResponse = await GenerateDirectAIResponseAsync(ctx, message, intent);

            // Log control metrics (no pipeline data)
            await LogMetricsAsync(ctx, startTime, null, null, null, null, null, null);

            return controlResponse;
        }

        // Treatment group: Run full naturalness pipeline
        Logger.LogInformation("Treatment group: Running full naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);

        var pipelineStartTime = DateTime.UtcNow;

        var history = GetHistory(ctx);
        var activeProducts = await GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = GetContactSummary(ctx);

        var earlyRagContext = await RetrieveRagContextAsync(ctx, message);
        var groundingContext = await _productGroundingService.BuildContextWithRelatedSuggestionsAsync(message, activeProducts, earlyRagContext.Products);
        if (groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts)
        {
            return await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);
        }

        history = _productGroundingService.SanitizeAssistantHistory(history, groundingContext.AllowedProducts).ToList();

        // Get VIP profile BEFORE building prompt
        var vipProfile = await GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;

        // Get returning customer flag from context
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;

        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
            Logger.LogInformation("First greeting sent for PSID: {PSID}", ctx.FacebookPSID);
        }

        // Build CTA context with intent awareness
        var ctaContext = BuildCtaContext(ctx, intent);

        // Detect emotion and generate tone profile
        var emotion = await EmotionDetectionService.DetectEmotionWithContextAsync(
            message,
            history.Select(h => new Services.AI.Models.ConversationMessage
            {
                Role = h.Role,
                Content = h.Content
            }).ToList(),
            CancellationToken.None);

        // Analyze conversation context
        var conversationContext = await ConversationContextAnalyzer.AnalyzeWithEmotionAsync(
            history.Select(h => new Services.AI.Models.ConversationMessage
            {
                Role = h.Role,
                Content = h.Content
            }).ToList(),
            new List<Services.Emotion.Models.EmotionScore> { emotion },
            CancellationToken.None);

        // Store context for decision-making
        ctx.SetData("conversationContext", conversationContext);

        Logger.LogInformation(
            "Conversation analysis - PSID: {PSID}, Stage: {Stage}, Quality: {Quality:F1}, Patterns: {PatternCount}, Insights: {InsightCount}",
            ctx.FacebookPSID,
            conversationContext.CurrentStage,
            conversationContext.Quality.Score,
            conversationContext.Patterns.Count,
            conversationContext.Insights.Count);

        var customer = await CustomerIntelligenceService.GetExistingAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"));

        var toneProfile = customer != null && vipProfile != null
            ? await ToneMatchingService.GenerateToneProfileAsync(
                emotion,
                vipProfile,
                customer,
                conversationTurnCount: history.Count,
                CancellationToken.None)
            : null;

        // Build tone instructions for prompt
        var toneInstruction = toneProfile != null
            ? $"""
## Tone Adaptation
Xung ho: {toneProfile.PronounText}
{string.Join("\n", toneProfile.ToneInstructions.Select(kv => $"- {kv.Value}"))}
"""
            : string.Empty;

        // Store tone profile in context for logging
        if (toneProfile != null)
        {
            ctx.SetData("toneProfile", toneProfile);
            ctx.SetData("emotionScore", emotion);

            Logger.LogInformation(
                "Tone profile generated for PSID: {PSID} - Emotion: {Emotion}, Tone: {Tone}, Pronoun: {Pronoun}, Escalation: {Escalation}",
                ctx.FacebookPSID,
                emotion.PrimaryEmotion,
                toneProfile.Level,
                toneProfile.PronounText,
                toneProfile.RequiresEscalation);
        }

        // Analyze for small talk
        var smallTalkResponse = toneProfile != null && vipProfile != null
            ? await SmallTalkService.AnalyzeAsync(
                message,
                emotion,
                toneProfile,
                conversationContext,
                vipProfile,
                isReturningCustomer,
                history.Count,
                CancellationToken.None)
            : null;

        // Store small talk response in context
        if (smallTalkResponse != null)
        {
            ctx.SetData("smallTalkResponse", smallTalkResponse);

            if (smallTalkResponse.IsSmallTalk)
            {
                Logger.LogInformation(
                    "Small talk detected for PSID: {PSID} - Intent: {Intent}, Confidence: {Confidence:F2}, Transition: {Transition}",
                    ctx.FacebookPSID,
                    smallTalkResponse.Intent,
                    smallTalkResponse.Confidence,
                    smallTalkResponse.TransitionReadiness);

                // For pure greetings with no business intent, return suggested response directly
                if (smallTalkResponse.TransitionReadiness == Services.SmallTalk.Models.TransitionReadiness.StayInSmallTalk &&
                    history.Count <= 2 &&
                    !string.IsNullOrWhiteSpace(smallTalkResponse.SuggestedResponse))
                {
                    var suggestedValidationContext = BuildFactValidationContext(
                        smallTalkResponse.SuggestedResponse,
                        toneProfile,
                        conversationContext,
                        smallTalkResponse,
                        message,
                        groundingContext.RequiresGrounding,
                        groundingContext.AllowedProducts,
                        allowPolicyFacts: false,
                        allowInventoryFacts: false,
                        allowOrderFacts: false);
                    var suggestedValidationResult = await ResponseValidationService.ValidateAsync(suggestedValidationContext, CancellationToken.None);
                    if (!suggestedValidationResult.IsValid)
                    {
                        Logger.LogWarning(
                            "Small talk suggested response validation failed for PSID {PSID}: {Issues}",
                            ctx.FacebookPSID,
                            string.Join("; ", suggestedValidationResult.Issues.Select(i => i.Message)));
                        return BuildProductGroundingFallbackReply();
                    }

                    AddToHistory(ctx, "assistant", smallTalkResponse.SuggestedResponse);
                    return smallTalkResponse.SuggestedResponse;
                }
            }
        }

        var ragContext = string.IsNullOrWhiteSpace(earlyRagContext.FormattedContext)
            ? null
            : earlyRagContext.FormattedContext;

        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
San pham duoc phep neu can neu ten: {FormatAllowedProductNames(groundingContext.AllowedProducts)}
Thong tin da co: {contactSummary}
{vipInstruction}

{toneInstruction}

Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.

{ctaContext}
""";

        var response = await GeminiService.SendMessageAsync(
            ctx.FacebookPSID,
            prompt,
            history,
            ragContext: ragContext);

        // Capture pipeline latency
        var pipelineLatency = (int)(DateTime.UtcNow - pipelineStartTime).TotalMilliseconds;

        // Validate response quality before sending to customer
        var validationContext = BuildFactValidationContext(
            response,
            toneProfile,
            conversationContext,
            smallTalkResponse,
            message,
            groundingContext.RequiresGrounding,
            groundingContext.AllowedProducts,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);

        var validationResult = await ResponseValidationService.ValidateAsync(validationContext, CancellationToken.None);

        if (!validationResult.IsValid)
        {
            Logger.LogWarning(
                "Response validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return BuildProductGroundingFallbackReply();
        }

        if (validationResult.Warnings.Any())
        {
            Logger.LogInformation(
                "Response validation warnings for PSID {PSID}: {Count} warnings",
                ctx.FacebookPSID,
                validationResult.Warnings.Count);
        }

        // Log treatment metrics (with pipeline data)
        await LogMetricsAsync(
            ctx,
            startTime,
            pipelineLatency,
            emotion?.PrimaryEmotion.ToString(),
            emotion != null ? (decimal)emotion.Confidence : null,
            toneProfile?.PronounText,
            conversationContext?.CurrentStage.ToString(),
            validationResult
        );

        // Validation: Log if CTA missing but trust AI to follow instruction
        var hasCtaKeywords = new[] { "gui", "len don", "dia chi", "so dien thoai", "xac nhan", "chon san pham" }
            .Any(keyword => response.ToLower().Contains(keyword));

        if (!hasCtaKeywords)
        {
            Logger.LogWarning(
                "Response may be missing CTA for {PSID}. AI should follow CTA instruction in prompt.",
                ctx.FacebookPSID
            );
        }

        return response;
    }

    private async Task<string> BuildGroundedRelatedSuggestionOrFallbackAsync(
        StateContext ctx,
        string message,
        GroundedProductContext groundingContext,
        Services.Tone.Models.ToneProfile? toneProfile,
        Services.Conversation.Models.ConversationContext? conversationContext,
        Services.SmallTalk.Models.SmallTalkResponse? smallTalkResponse)
    {
        if (!groundingContext.HasRelatedSuggestions || string.IsNullOrWhiteSpace(groundingContext.RelatedSuggestionReply))
        {
            return groundingContext.FallbackReply;
        }

        var validationContext = BuildFactValidationContext(
            groundingContext.RelatedSuggestionReply,
            toneProfile,
            conversationContext,
            smallTalkResponse,
            message,
            requiresProductGrounding: true,
            groundingContext.RelatedSuggestions,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);

        var validationResult = await ResponseValidationService.ValidateAsync(validationContext, CancellationToken.None);
        if (!validationResult.IsValid)
        {
            Logger.LogWarning(
                "Related product suggestion validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return groundingContext.FallbackReply;
        }

        return groundingContext.RelatedSuggestionReply;
    }

    private async Task<RAGContext> RetrieveRagContextAsync(StateContext ctx, string message)
    {
        if (!RagOptions.Enabled || RagService == null)
        {
            return new RAGContext(string.Empty, new List<string>(), new List<GroundedProduct>(), new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "disabled"));
        }

        try
        {
            var ragResult = await RagService.RetrieveContextAsync(message, topK: RagOptions.TopK);

            Logger.LogInformation(
                "RAG retrieved {Count} products in {Ms}ms for PSID: {PSID}",
                ragResult.ProductIds.Count,
                ragResult.Metrics.TotalLatency.TotalMilliseconds,
                ctx.FacebookPSID);

            return ragResult;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RAG retrieval failed for PSID: {PSID}", ctx.FacebookPSID);
            return new RAGContext(string.Empty, new List<string>(), new List<GroundedProduct>(), new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "error"));
        }
    }

    /// <summary>
    /// Control group: Direct AI response without naturalness pipeline.
    /// </summary>
    private async Task<string> GenerateDirectAIResponseAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null)
    {
        var history = GetHistory(ctx);
        var activeProducts = await GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = GetContactSummary(ctx);

        // Get VIP profile for customer instruction only
        var vipProfile = await GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
        }

        // Build CTA context
        var ctaContext = BuildCtaContext(ctx, intent);

        // RAG context retrieval if enabled
        string? ragContext = null;
        var ragProducts = new List<GroundedProduct>();
        if (RagOptions.Enabled && RagService != null)
        {
            try
            {
                var ragResult = await RagService.RetrieveContextAsync(
                    message,
                    topK: RagOptions.TopK);

                ragContext = ragResult.FormattedContext;
                ragProducts = ragResult.Products;

                Logger.LogInformation(
                    "RAG retrieved {Count} products in {Ms}ms for PSID: {PSID} (control group)",
                    ragResult.ProductIds.Count,
                    ragResult.Metrics.TotalLatency.TotalMilliseconds,
                    ctx.FacebookPSID);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "RAG retrieval failed for control group PSID: {PSID}", ctx.FacebookPSID);
            }
        }

        var groundingContext = await _productGroundingService.BuildContextWithRelatedSuggestionsAsync(message, activeProducts, ragProducts);
        if (groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts)
        {
            return await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);
        }

        history = _productGroundingService.SanitizeAssistantHistory(history, groundingContext.AllowedProducts).ToList();

        // Simple prompt without tone/emotion/context instructions
        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
San pham duoc phep neu can neu ten: {FormatAllowedProductNames(groundingContext.AllowedProducts)}
Thong tin da co: {contactSummary}
{vipInstruction}

Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.

{ctaContext}
""";

        var response = await GeminiService.SendMessageAsync(
            ctx.FacebookPSID,
            prompt,
            history,
            ragContext: ragContext);

        var validationContext = BuildFactValidationContext(
            response,
            null,
            null,
            null,
            message,
            groundingContext.RequiresGrounding,
            groundingContext.AllowedProducts,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);
        var validationResult = await ResponseValidationService.ValidateAsync(validationContext, CancellationToken.None);
        if (!validationResult.IsValid)
        {
            Logger.LogWarning(
                "Direct AI response validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return BuildProductGroundingFallbackReply();
        }

        return response;
    }

    private static ResponseValidationContext BuildFactValidationContext(
        string response,
        Services.Tone.Models.ToneProfile? toneProfile,
        Services.Conversation.Models.ConversationContext? conversationContext,
        Services.SmallTalk.Models.SmallTalkResponse? smallTalkResponse,
        string customerMessage,
        bool requiresProductGrounding,
        IReadOnlyCollection<GroundedProduct> products,
        bool allowPolicyFacts,
        bool allowInventoryFacts,
        bool allowOrderFacts)
    {
        return new ResponseValidationContext
        {
            Response = response,
            ToneProfile = toneProfile ?? new Services.Tone.Models.ToneProfile(),
            ConversationContext = conversationContext ?? new Services.Conversation.Models.ConversationContext(),
            SmallTalkResponse = smallTalkResponse,
            RequiresFactGrounding = requiresProductGrounding || products.Count > 0,
            AllowedProductNames = products.Select(product => product.Name).ToList(),
            AllowedProductCodes = products.Select(product => product.Code).ToList(),
            AllowedPrices = products.Where(product => product.Price.HasValue).Select(product => product.Price!.Value).ToList(),
            AllowPolicyFacts = allowPolicyFacts,
            AllowInventoryFacts = allowInventoryFacts,
            AllowOrderFacts = allowOrderFacts
        };
    }

    private static string FormatAllowedProductNames(IReadOnlyCollection<GroundedProduct> products)
    {
        return products.Count == 0
            ? "khong co"
            : string.Join(", ", products.Select(product => $"{product.Name} ({product.Code})"));
    }

    private async Task<VipProfile?> GetVipProfileAsync(StateContext ctx)
    {
        var customer = await CustomerIntelligenceService.GetExistingAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"))
            ?? await CustomerIntelligenceService.GetOrCreateAsync(
                ctx.FacebookPSID,
                ctx.GetData<string>("facebookPageId"),
                ctx.GetData<string>("customerPhone"));

        if (customer == null)
            return null;

        return await CustomerIntelligenceService.GetVipProfileAsync(customer);
    }

    private static string BuildCustomerInstruction(VipProfile? vipProfile, bool shouldGreet, bool isReturningCustomer)
    {
        if (shouldGreet)
        {
            if (vipProfile?.IsVip == true)
            {
                return $@"Khach hang VIP (khach cu da co {vipProfile.TotalOrders} don hang):
- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay
- Chao hoi am ap, than mat, tu nhien nhu cham soc khach quen
- KHONG gioi thieu lai san pham hoac page
- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi nhu cau hien tai
- Co the dung mau: ""Hom nay chi dang can em tu van gi a?"" hoac ""Dot nay chi dang quan tam san pham nao a?""
- CHI dung xung ho than thien 1 lan o tin nhan chao dau tien";
            }

            if (vipProfile?.Tier == VipTier.Returning || isReturningCustomer)
            {
                return $@"Khach cu (da mua {vipProfile?.TotalOrders ?? 0} don):
- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay
- Chao nhe nhang, than thien, tu nhien hon kieu mau may moc
- KHONG gioi thieu lai catalog
- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi khach dang can gi de tu van
- Co the dung mau: ""Hom nay chi dang can em tu van gi a?"" hoac ""Chi dang tim san pham nao de em goi y nhanh a?""
- Tin nhan chao phai vua co loi chao vua co cau hoi nhu cau, khong duoc chao xong bo ngo";
            }

            return @"Khach moi - tin nhan dau tien:
- Chao tu nhien, mem va giong nhan vien cham soc khach hang that
- KHONG chao kho cung, KHONG chi chao roi dung lai
- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi khach dang can gi de tu van
- Co the dung mau: ""Dạ em chào chị, hôm nay chị đang cần em tư vấn gì ạ?"" hoac ""Chào chị nha, chị đang quan tâm sản phẩm nào để em tư vấn nhanh cho mình ạ?""
- Khong tu y gioi thieu dai dong catalog neu khach chi moi chao";
        }

        if (vipProfile?.IsVip == true)
        {
            return $@"Khach hang VIP (da mua {vipProfile.TotalOrders} don) - DA CHAO ROI:
- KHONG chao lai
- Chi tra loi cau hoi va ho tro khach ngan gon, tu nhien
- CTA bien the hoa, khong lap lai cung cau hoi lien tiep";
        }

        if (vipProfile?.Tier == VipTier.Returning || isReturningCustomer)
        {
            return @"Khach cu - DA CHAO ROI:
- CHI TRA LOI CAU HOI, khong chao lai
- Dung giong binh thuong, ngan gon, tu nhien
- CTA bien the hoa neu can, tranh lap lai cung 1 cau hoi";
        }

        return @"Khach moi sau tin chao dau:
- Chi tra loi dung cau hoi hien tai, khong chao lai
- Giong tu nhien, ngan gon, ro y";
    }

    private static string BuildCtaContext(StateContext ctx, Services.AI.Models.CustomerIntent? intent = null)
    {
        var hasProduct = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        var consultationDeclined = ctx.GetData<bool?>("consultationDeclined") == true;
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;
        var missingInfo = GetMissingContactInfo(ctx);
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
        var messageCount = history.Count(m => m.Role == "user");

        // Case 0: Customer declined consultation - stop asking
        if (consultationDeclined && hasProduct)
        {
            if (missingInfo.Count == 0)
            {
                return "CTA Instruction: Customer declined consultation. Create order immediately. Use: \"Vậy là mình chốt đơn này luôn nha chị. Em lên đơn ngay.\"";
            }

            var missing = string.Join(" va ", missingInfo);
            return $"CTA Instruction: Customer declined consultation. Ask for missing info ({missing}) to complete order. DO NOT ask about consultation again.";
        }

        // Case 0.5: Already rejected consultation 2+ times - don't repeat
        if (rejectionCount >= 2 && hasProduct)
        {
            return "CTA Instruction: Customer rejected consultation twice. DO NOT ask again. Move to order closing or ask for missing contact info.";
        }

        // Case 1: Existing customer with data loaded from DB - only confirm after clear buy intent
        if (needsConfirmation && missingInfo.Count == 0)
        {
            var phone = ctx.GetData<string>("customerPhone");
            var address = ctx.GetData<string>("shippingAddress");

            if (intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                intent == Services.AI.Models.CustomerIntent.Confirming)
            {
                return $"""
CTA Instruction: Customer is returning and already wants to buy. Ask them to confirm their existing phone and address before creating the order.
- Use concise wording like: "Chi xac nhan giup em SDT {phone} va dia chi {address} con dung khong a?"
- DO NOT say the order is being created yet.
""";
            }

            return $"""
CTA Instruction: Customer is still consulting. We have previous contact info (SDT {phone}, dia chi {address}) but DO NOT push for confirmation yet.
- Answer their question first
- No closing CTA while they are still asking
""";
        }

        // Case 2: All info collected and confirmed - create order only when customer is already in buy path
        if (missingInfo.Count == 0 && !needsConfirmation)
        {
            return """
CTA Instruction: Customer already provided and confirmed all contact information.
- If they are explicitly buying, tell them you will create the order now.
- If they are still asking questions, continue answering and do NOT add closing CTA.
""";
        }

        // NEW: Giai đoạn tư vấn (1-2 câu đầu) - KHÔNG CẦN CTA
        if (messageCount <= 2 && intent == Services.AI.Models.CustomerIntent.Questioning)
        {
            return """
CTA Instruction: Customer is in consultation phase (asking questions). Answer naturally WITHOUT pushing for order.
- Just answer the question directly
- DO NOT add CTA like "Chị chọn sản phẩm và gửi thông tin"
- Let customer continue asking questions naturally
""";
        }

        // NEW: Giai đoạn chuyển tiếp (3-4 câu) - Gợi ý nhẹ nhàng
        if (messageCount >= 3 && messageCount <= 4 && !hasProduct)
        {
            return """
CTA Instruction: Customer has asked 3-4 questions. Gently suggest next step.
- Use soft prompts like "Chị quan tâm mẫu này ạ?" or "Chị muốn em tư vấn thêm gì không ạ?"
- DO NOT push hard for order yet
""";
        }

        // Case 3: Customer shows buying intent (ReadyToBuy) - move into contact collection/confirmation
        if (intent == Services.AI.Models.CustomerIntent.ReadyToBuy && hasProduct)
        {
            var missing = string.Join(" va ", missingInfo);
            return $"""
CTA Instruction: Customer is ready to buy.
- Ask only for missing contact fields or ask to confirm remembered contact.
- DO NOT use broad closing lines like "len don ngay" before contact is complete and confirmed.
- Missing info: {missing}
""";
        }

        // Case 4: Has product but missing some contact info - ask for missing pieces
        if (hasProduct && missingInfo.Count > 0)
        {
            var missing = string.Join(" va ", missingInfo);
            return $"""
CTA Instruction: Naturally ask customer to provide missing info ({missing}) to complete the order. Use friendly tone like "Chi gui em {missing} de em len don nha" or "Em can {missing} cua chi de len don a".
""";
        }

        // Case 5: No product selected yet - ask to choose product (only after 3+ messages)
        if (messageCount >= 3)
        {
            return """
CTA Instruction: Naturally ask customer to choose a product. Use friendly tone like "Chị quan tâm sản phẩm nào ạ?" or "Chị muốn em tư vấn thêm không ạ?".
""";
        }

        // Default: No CTA needed (early consultation phase)
        return """
CTA Instruction: Customer is in early consultation phase. Answer questions naturally WITHOUT CTA.
""";
    }

    private async Task<string?> BuildProductConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        ctx.SetData("inventory_confirmed", false);

        var lines = new List<string>
        {
            $"Dạ {product.Name} bên em là {NormalizeSentence(product.Description)}"
        };

        if (product.Code.Equals("KCN", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Sản phẩm này hợp khi chị hay đi ngoài trời vì ưu tiên bảo vệ da trước nắng và tia UV.");
        }
        else if (product.Code.Equals("KL", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Dòng này thiên về cấp ẩm và giữ da mềm hơn, hợp khi da dễ khô hoặc thiếu ẩm do nắng gió.");
        }
        else if (product.Code.Equals("MN", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Dòng này thiên về dưỡng ẩm và phục hồi da qua đêm, hợp khi da đang khô hoặc thiếu ẩm.");
        }

        lines.Add($"Nếu chị muốn em nói kỹ hơn về công dụng chính hoặc cách dùng của {product.Name} thì em tư vấn tiếp ạ.");
        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string?> BuildShippingConsultationReplyAsync(StateContext ctx, string message)
    {
        var lockedProductCode = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).FirstOrDefault();
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        var effectiveProductCode = product?.Code ?? lockedProductCode;
        if (string.IsNullOrWhiteSpace(effectiveProductCode) || product == null)
        {
            return null;
        }

        var productCodes = new List<string> { effectiveProductCode };
        ctx.SetData("selectedProductCodes", productCodes);
        await SyncActiveProductPolicyContextAsync(ctx, effectiveProductCode);

        var snapshot = await BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
        ctx.SetData("shipping_policy_confirmed", false);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("inventory_confirmed", snapshot?.InventoryConfirmed == true);

        var shippingMessage = "em chưa dám chốt freeship hay phí ship ngay lúc này, để em kiểm tra lại theo đơn cụ thể rồi báo chị chính xác ạ";
        var giftMessage = snapshot?.GiftConfirmed == true
            ? $"Theo dữ liệu nội bộ hiện tại thì quà tặng đang gắn với sản phẩm này là {snapshot.GiftName} ạ."
            : "Hiện tại em chưa thấy quà tặng nào được xác nhận rõ cho sản phẩm này ạ.";
        var productLabel = product.Name;

        return $"Dạ với {productLabel}, {shippingMessage} {giftMessage} Nếu chị cần em tính lại theo đơn cụ thể hoặc hỗ trợ chốt đơn thì em làm tiếp cho mình nha.";
    }

    private async Task<string?> BuildContactMemoryReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        var productName = product?.Name;
        var hasPhone = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hasAddress = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        if (hasPhone && hasAddress)
        {
            if (needsConfirmation)
            {
                var phone = ctx.GetData<string>("customerPhone");
                var address = ctx.GetData<string>("shippingAddress");
                return string.IsNullOrWhiteSpace(productName)
                    ? $"Dạ em đang có thông tin cũ của chị rồi ạ. Chị giúp em xác nhận lại SĐT {phone} và địa chỉ {address} còn dùng đúng không ạ?"
                    : $"Dạ em đang có sẵn thông tin giao hàng rồi ạ. Nếu mình chốt {productName} thì chị giúp em xác nhận lại SĐT {phone} và địa chỉ {address} còn dùng đúng không ạ?";
            }

            return string.IsNullOrWhiteSpace(productName)
                ? "Dạ em đang có đủ thông tin giao hàng của chị rồi ạ. Khi chị chốt sản phẩm em lên đơn ngay cho mình nha."
                : $"Dạ em đang có đủ thông tin để chốt {productName} cho chị rồi ạ. Nếu chị đồng ý em lên đơn theo thông tin này cho mình nha.";
        }

        var missing = string.Join(" và ", GetMissingContactInfo(ctx).Select(x => x == "so dien thoai" ? "số điện thoại" : "địa chỉ"));
        return string.IsNullOrWhiteSpace(productName)
            ? $"Dạ em chưa đủ thông tin của chị ạ. Chị gửi em {missing} giúp em nha."
            : $"Dạ để em chốt đúng {productName} cho chị thì chị gửi em {missing} giúp em nha.";
    }

    private async Task<string?> BuildContactCollectionReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        var productName = product?.Name;
        var missingInfo = GetMissingContactInfo(ctx);
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        if (needsConfirmation && missingInfo.Count == 0)
        {
            var phone = ctx.GetData<string>("customerPhone");
            var address = ctx.GetData<string>("shippingAddress");
            return string.IsNullOrWhiteSpace(productName)
                ? $"Dạ em đang có thông tin giao hàng lần trước của chị rồi ạ. Chị giúp em xác nhận SĐT {phone} và địa chỉ {address} còn dùng đúng không, hay chị muốn đổi thông tin mới để em lên đơn cho lần này ạ?"
                : $"Dạ em đang có sẵn thông tin để chốt {productName} cho chị rồi ạ. Chị giúp em xác nhận SĐT {phone} và địa chỉ {address} còn dùng đúng không, hay chị muốn đổi thông tin mới để em lên đơn cho lần này ạ?";
        }

        if (missingInfo.Count == 0)
        {
            return null;
        }

        var missing = string.Join(" và ", missingInfo.Select(x => x == "so dien thoai" ? "số điện thoại" : "địa chỉ"));
        return string.IsNullOrWhiteSpace(productName)
            ? $"Dạ chị gửi em {missing} để em lên đơn cho mình nha."
            : $"Dạ chị gửi em {missing} để em chốt {productName} cho mình nha.";
    }

    private string BuildPendingContactClarificationReply(StateContext ctx)
    {
        var phone = ctx.GetData<string>("customerPhone");
        var address = ctx.GetData<string>("shippingAddress");
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasAddress = !string.IsNullOrWhiteSpace(address);

        if (hasPhone && hasAddress)
        {
            return $"Dạ em đang giữ SĐT {phone} và địa chỉ {address} từ lần trước của chị ạ. Chị giúp em xác nhận là vẫn dùng đúng 2 thông tin này, hoặc gửi thông tin mới để em cập nhật cho đơn lần này nha.";
        }

        if (hasPhone)
        {
            return $"Dạ em đang giữ SĐT {phone} từ lần trước của chị ạ. Chị giúp em xác nhận số này còn dùng đúng không, rồi gửi em thêm địa chỉ giao hàng hiện tại để em chốt đơn cho mình nha.";
        }

        if (hasAddress)
        {
            return $"Dạ em đang giữ địa chỉ {address} từ lần trước của chị ạ. Chị giúp em xác nhận địa chỉ này còn dùng đúng không, rồi gửi em thêm số điện thoại để em chốt đơn cho mình nha.";
        }

        return "Dạ chị giúp em gửi lại số điện thoại và địa chỉ giao hàng hiện tại để em chốt đơn cho mình nha.";
    }

    private async Task<string?> BuildOrderEstimateReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        await RefreshSelectedProductPolicyContextAsync(ctx, message);

        var quantities = ctx.GetData<Dictionary<string, int>>("selectedProductQuantities")
                         ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var quantity = quantities.TryGetValue(product.Code, out var selectedQuantity) && selectedQuantity > 0
            ? selectedQuantity
            : 1;

        var giftName = ctx.GetData<string>("selectedGiftName");
        var merchandiseTotal = product.BasePrice * quantity;
        var totalProducts = quantity + (string.IsNullOrWhiteSpace(giftName) ? 0 : 1);
        var unitLabel = quantity > 1 ? $"{quantity} hũ {product.Name}" : $"1 hũ {product.Name}";
        var giftLabel = string.IsNullOrWhiteSpace(giftName) ? string.Empty : $" + 1 quà tặng {giftName}";

        return $"Dạ nếu mình chốt {product.Name} thì đơn đang có tổng cộng {totalProducts} sản phẩm gồm {unitLabel}{giftLabel} ạ. Tạm tính tiền hàng hiện tại là {merchandiseTotal:N0}đ, còn phí ship và tổng đơn cuối em cần kiểm tra lại theo đơn cụ thể rồi báo chị chính xác nha.";
    }

    private async Task<Product?> ResolveCurrentProductAsync(StateContext ctx, string message)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        Product? activeProduct = null;
        if (selectedCodes.Count > 0)
        {
            activeProduct = await ProductMappingService.GetActiveProductByCodeAsync(selectedCodes[0]);
            if (activeProduct != null)
            {
                var directProduct = await ProductMappingService.GetProductByMessageAsync(message);
                if (directProduct != null &&
                    !string.Equals(directProduct.Code, activeProduct.Code, StringComparison.OrdinalIgnoreCase) &&
                    ShouldSwitchActiveProduct(message, activeProduct, directProduct))
                {
                    await ApplyResolvedProductAsync(ctx, directProduct, "explicit-switch");
                    return directProduct;
                }

                return activeProduct;
            }
        }

        var matchedProduct = await ProductMappingService.GetProductByMessageAsync(message);
        if (matchedProduct != null)
        {
            await ApplyResolvedProductAsync(ctx, matchedProduct, "direct-message");
            return matchedProduct;
        }

        await TryExtractProductFromHistoryAsync(ctx);
        selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count == 0)
        {
            return null;
        }

        return await ProductMappingService.GetActiveProductByCodeAsync(selectedCodes[0]);
    }

    private static bool ShouldSwitchActiveProduct(string message, Product activeProduct, Product directProduct)
    {
        var normalized = NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var hasReplacementSignal = normalized.Contains("doi sang", StringComparison.Ordinal)
            || normalized.Contains("chuyen sang", StringComparison.Ordinal)
            || normalized.Contains("thay cho", StringComparison.Ordinal)
            || normalized.Contains("khong lay", StringComparison.Ordinal)
            || normalized.Contains("khong mua", StringComparison.Ordinal)
            || normalized.Contains("lay thay", StringComparison.Ordinal)
            || normalized.Contains("chot lai", StringComparison.Ordinal);

        if (hasReplacementSignal)
        {
            return true;
        }

        var hasStrongCommitmentSignal = normalized.Contains("mua ", StringComparison.Ordinal)
            || normalized.Contains("muon mua", StringComparison.Ordinal)
            || normalized.Contains("chot", StringComparison.Ordinal)
            || normalized.Contains("lay ", StringComparison.Ordinal)
            || normalized.Contains("chot don", StringComparison.Ordinal)
            || normalized.Contains("len don", StringComparison.Ordinal);

        if (!hasStrongCommitmentSignal)
        {
            return false;
        }

        if (HasPolicyOrComparisonSignal(normalized))
        {
            return false;
        }

        var mentionsActiveProduct = ReferencesProduct(normalized, activeProduct);
        var mentionsDirectProduct = ReferencesProduct(normalized, directProduct);
        if (!mentionsDirectProduct)
        {
            return false;
        }

        if (mentionsActiveProduct)
        {
            return false;
        }

        return HasDirectProductCommitment(normalized, directProduct);
    }

    private async Task<Product?> TryResolveNumberedSuggestionSelectionAsync(StateContext ctx, string? currentMessage)
    {
        var recentMessages = GetHistory(ctx).TakeLast(10).ToList();
        var numberedSuggestion = await ResolveNumberedAssistantSuggestionAsync(recentMessages, currentMessage);
        if (numberedSuggestion == null)
        {
            return null;
        }

        Logger.LogInformation(
            "Extracted product {ProductName} (Code: {ProductCode}) from numbered assistant suggestion for PSID: {PSID}",
            numberedSuggestion.Name,
            numberedSuggestion.Code,
            ctx.FacebookPSID);

        await ApplyResolvedProductAsync(ctx, numberedSuggestion, "numbered-suggestion");
        return numberedSuggestion;
    }

    private async Task<Product?> ResolveNumberedAssistantSuggestionAsync(
        List<AiConversationMessage> recentMessages,
        string? currentMessage)
    {
        var selectedNumber = ExtractRelatedSuggestionSelectionNumber(currentMessage);
        if (!selectedNumber.HasValue)
        {
            return null;
        }

        foreach (var msg in recentMessages
                     .Where(x => string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                     .Reverse())
        {
            var numberedLines = msg.Content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsNumberedSuggestionLine)
                .ToList();

            if (numberedLines.Count == 0)
            {
                continue;
            }

            var selectedLine = numberedLines.FirstOrDefault(line => line.StartsWith($"{selectedNumber.Value})", StringComparison.Ordinal));
            return selectedLine == null
                ? null
                : await ProductMappingService.GetProductByMessageAsync(selectedLine);
        }

        return null;
    }

    private async Task<List<HistoryProductCandidate>> CollectHistoryProductCandidatesAsync(
        List<AiConversationMessage> recentMessages,
        string role)
    {
        var candidates = new List<HistoryProductCandidate>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var msg in recentMessages
                     .Where(x => string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase))
                     .Reverse())
        {
            var product = await ProductMappingService.GetProductByMessageAsync(msg.Content);
            if (product == null || !seenCodes.Add(product.Code))
            {
                continue;
            }

            candidates.Add(new HistoryProductCandidate(product, msg.Role, msg.Content));
        }

        return candidates;
    }

    private async Task<HistoryProductCandidate?> ResolveAmbiguousHistoryProductCandidateAsync(
        StateContext ctx,
        List<AiConversationMessage> recentMessages,
        List<HistoryProductCandidate> candidates,
        string preferredRole)
    {
        var candidateProducts = candidates
            .Select(candidate => new GroundedProduct(
                candidate.Product.Id,
                candidate.Product.Code,
                candidate.Product.Name,
                candidate.Product.Category.ToString(),
                candidate.Product.BasePrice))
            .ToList();
        var sanitizedMessages = _productGroundingService.SanitizeAssistantHistory(recentMessages, candidateProducts).ToList();
        var candidateSummary = string.Join(", ", candidates.Select(x => $"{x.Product.Name} ({x.Product.Code})"));
        var historySummary = string.Join("\n", sanitizedMessages.Select(x => $"{x.Role}: {x.Content}"));
        var prompt = $"""
Chọn đúng 1 mã sản phẩm khách đang muốn mua nhất từ lịch sử chat gần đây.
Ưu tiên message mới hơn và ưu tiên message từ user hơn assistant.
Chỉ trả về đúng 1 product code trong danh sách này: {string.Join(", ", candidates.Select(x => x.Product.Code))}
Nếu chưa chắc, vẫn chọn mã có khả năng cao nhất theo ưu tiên trên.
Preferred role: {preferredRole}
Candidates: {candidateSummary}
History:
{historySummary}
""";

        var aiResponse = await GeminiService.SendMessageAsync(
            ctx.FacebookPSID,
            prompt,
            sanitizedMessages,
            Services.AI.Models.GeminiModelType.FlashLite,
            cancellationToken: CancellationToken.None);

        var matchedCandidate = candidates.FirstOrDefault(x => aiResponse.Contains(x.Product.Code, StringComparison.OrdinalIgnoreCase));
        if (matchedCandidate != null)
        {
            Logger.LogInformation(
                "Resolved ambiguous history product via AI for PSID: {PSID}. ProductCode: {ProductCode}",
                ctx.FacebookPSID,
                matchedCandidate.Product.Code);
        }

        return matchedCandidate;
    }

    private async Task ApplyResolvedProductAsync(StateContext ctx, Product product, string source)
    {
        ctx.SetData("selectedProductCodes", new List<string> { product.Code });
        ctx.SetData("lastResolvedProductCode", product.Code);
        ctx.SetData("lastResolvedProductSource", source);
        var gift = await GiftSelectionService.SelectGiftForProductAsync(product.Code);
        var shippingFee = FreeshipCalculator.CalculateShippingFee(new List<string> { product.Code });
        ctx.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
        ctx.SetData("selectedGiftName", gift?.Name ?? string.Empty);
        ctx.SetData("shippingFee", shippingFee);
    }

    private async Task<List<Product>> GetActiveSelectedProductsAsync(StateContext ctx)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        var activeProducts = new List<Product>();

        foreach (var productCode in selectedCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var product = await ProductMappingService.GetActiveProductByCodeAsync(productCode);
            if (product != null)
            {
                activeProducts.Add(product);
            }
        }

        return activeProducts;
    }

    private async Task<Product?> GetActiveProductOrResolveAsync(StateContext ctx, string message)
    {
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count > 0)
        {
            var activeProduct = await ProductMappingService.GetActiveProductByCodeAsync(selectedCodes[0]);
            if (activeProduct != null)
            {
                var directProduct = await ProductMappingService.GetProductByMessageAsync(message);
                if (directProduct == null ||
                    string.Equals(directProduct.Code, activeProduct.Code, StringComparison.OrdinalIgnoreCase) ||
                    !ShouldSwitchActiveProduct(message, activeProduct, directProduct))
                {
                    return activeProduct;
                }
            }
        }

        return await ResolveCurrentProductAsync(ctx, message);
    }

    private async Task SyncActiveProductPolicyContextAsync(StateContext ctx, string productCode)
    {
        Gift? gift = null;
        var giftTask = GiftSelectionService.SelectGiftForProductAsync(productCode);
        if (giftTask != null)
        {
            gift = await giftTask;
        }

        ctx.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
        ctx.SetData("selectedGiftName", gift?.Name ?? string.Empty);
        ctx.SetData("shippingFee", FreeshipCalculator.CalculateShippingFee(new List<string> { productCode }));
    }

    private async Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotAsync(StateContext ctx, Product product)
    {
        var selectedVariantId = ctx.GetData<string>("selectedVariantId");
        var selectedVariant = product.Variants.FirstOrDefault(v =>
            string.Equals(v.Id, selectedVariantId, StringComparison.OrdinalIgnoreCase));
        var gift = await GiftSelectionService.SelectGiftForProductAsync(product.Code);

        return CommercialFactSnapshot.Create(
            product,
            selectedVariant,
            gift,
            ctx.GetData<decimal?>("shippingFee"),
            false);
    }

    private async Task<CommercialFactSnapshot?> BuildCommercialFactSnapshotForPolicyAsync(StateContext ctx, Product product)
    {
        await RefreshSelectedProductPolicyContextAsync(ctx, product.Code);
        var baseSnapshot = await BuildCommercialFactSnapshotAsync(ctx, product);
        if (baseSnapshot == null)
        {
            return null;
        }

        return baseSnapshot with
        {
            ShippingFee = null,
            ShippingConfirmed = false,
            IsFreeship = null
        };
    }

    private async Task RefreshSelectedProductPolicyContextAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        var productCode = product?.Code ?? (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(productCode))
        {
            return;
        }

        ctx.SetData("selectedProductCodes", new List<string> { productCode });
        await SyncActiveProductPolicyContextAsync(ctx, productCode);
    }

    protected async Task<string?> TryCreateDraftConfirmationAsync(StateContext ctx, string message)
    {
        await RefreshSelectedProductPolicyContextAsync(ctx, message);

        var draft = await DraftOrderCoordinator.FinalizeDraftOrderAsync(ctx);
        if (draft == null)
        {
            return null;
        }

        ctx.CurrentState = ConversationState.Complete;
        return BuildDraftConfirmation(ctx, draft);
    }

    private string BuildPolicyGiftMessage(StateContext ctx)
    {
        var giftName = ctx.GetData<string>("selectedGiftName");
        if (string.IsNullOrWhiteSpace(giftName))
        {
            return "Hiện tại đơn này chưa có quà tặng theo chính sách đang áp dụng ạ.";
        }

        return $"Nếu chốt đơn lúc này thì quà tặng theo chính sách hiện tại là {giftName} ạ.";
    }

    private sealed record HistoryProductCandidate(Product Product, string Role, string Message);

    private async Task<string> BuildFirstGreetingReplyAsync(StateContext ctx)
    {
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var customerName = ctx.GetData<string>("customerName") ?? ctx.GetData<string>("rememberedCustomerName");
        var vipProfile = await GetVipProfileAsync(ctx);

        if (vipProfile?.IsVip == true)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                ? $"Dạ em chào chị {customerName} ạ, lâu rồi mới thấy chị ghé lại. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?"
                : "Dạ em chào chị ạ, lâu rồi mới thấy chị ghé lại. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?";
        }

        if (isReturningCustomer)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                ? $"Dạ em chào chị {customerName} ạ, em rất vui được hỗ trợ chị lại nè. Hôm nay chị đang cần em tư vấn sản phẩm nào ạ?"
                : "Dạ em chào chị ạ, em rất vui được hỗ trợ chị lại nè. Hôm nay chị đang cần em tư vấn sản phẩm nào ạ?";
        }

        return "Dạ em chào chị ạ. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?";
    }

    private static bool IsPureGreeting(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized is "hi"
            or "hello"
            or "alo"
            or "alô"
            or "chao"
            or "chào"
            or "hi shop"
            or "hi sop"
            or "hi sốp"
            or "hello shop"
            or "chao shop"
            or "chào shop"
            or "chao sop"
            or "chào sốp"
            or "chao em"
            or "chào em";
    }

    private static bool ContainsAnyPhrase(string message, params string[] phrases)
    {
        return phrases.Any(phrase => message.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresProductGrounding(
        string message,
        bool isProductQuestion,
        bool isPriceQuestion,
        bool isInventoryQuestion,
        bool isPolicyQuestion,
        bool isQuestioning,
        bool hasQuestionMarker,
        ConversationState currentState)
    {
        if (isPriceQuestion || isInventoryQuestion || isProductQuestion || IsCatalogListingQuestion(message))
        {
            return true;
        }

        return isQuestioning &&
               !isPolicyQuestion &&
               (hasQuestionMarker || currentState == ConversationState.Consulting) &&
               HasProductCategoryReference(message);
    }

    private static bool RequiresProductGrounding(string message)
    {
        return IsCatalogListingQuestion(message) ||
               HasProductCategoryReference(message) && ContainsAnyPhrase(message,
                   "giá", "gia", "công dụng", "cong dung", "tác dụng", "tac dung", "thành phần", "thanh phan",
                   "cách dùng", "cach dung", "còn hàng", "con hang", "hết hàng", "het hang", "tồn kho", "ton kho");
    }

    private static bool IsCatalogListingQuestion(string message)
    {
        if (ContainsAnyPhrase(message, "catalog", "danh sách", "danh sach", "sản phẩm nào", "san pham nao"))
        {
            return true;
        }

        var hasListingIntent = ContainsAnyPhrase(message,
            "các loại", "cac loai", "có loại nào", "co loai nao", "có những loại", "co nhung loai",
            "loại nào", "loai nao", "dòng nào", "dong nao", "mẫu nào", "mau nao");
        var asksShopHas = ContainsAnyPhrase(message, "bên em có", "ben em co", "shop có", "shop co");

        return hasListingIntent && (asksShopHas || HasProductCategoryReference(message))
            || asksShopHas && HasProductCategoryReference(message);
    }

    private static bool HasProductCategoryReference(string message)
    {
        return ContainsAnyPhrase(message,
            "sản phẩm", "san pham", "mặt nạ", "mat na", "kem", "serum", "toner", "sữa rửa mặt", "sua rua mat",
            "chống nắng", "chong nang", "dưỡng ẩm", "duong am", "mỹ phẩm", "my pham");
    }

    private static string BuildProductGroundingFallbackReply()
    {
        return ProductGroundingService.FallbackReply;
    }

    private static bool IsGenericBuyContinuationWhileAwaitingContactConfirmation(string message)
    {
        var normalized = NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Contains("dung roi", StringComparison.Ordinal)
            || normalized.Contains("van dung", StringComparison.Ordinal)
            || normalized.Contains("nhu cu", StringComparison.Ordinal)
            || normalized.Contains("thong tin cu", StringComparison.Ordinal)
            || normalized.Contains("cu nhu vay", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized == "ok"
               || normalized == "oke"
               || normalized == "okay"
               || normalized == "ok e"
               || normalized == "ok em"
               || normalized == "oke e"
               || normalized == "oke em"
               || normalized.Contains("len don", StringComparison.Ordinal)
               || normalized.Contains("chot", StringComparison.Ordinal)
               || normalized.Contains("dat hang", StringComparison.Ordinal)
               || normalized.Contains("dat luon", StringComparison.Ordinal)
               || normalized.Contains("mua luon", StringComparison.Ordinal)
               || normalized.Contains("lay san pham nay", StringComparison.Ordinal)
               || normalized.Contains("lay nha", StringComparison.Ordinal)
               || normalized.Contains("lay nhe", StringComparison.Ordinal);
    }

    private static string NormalizeSentence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "sản phẩm chăm sóc da được nhiều chị quan tâm ạ.";
        }

        var normalized = text.Trim().TrimEnd('.', '!', '?');
        return normalized.EndsWith("ạ", StringComparison.OrdinalIgnoreCase)
            ? normalized + "."
            : normalized + " ạ.";
    }

    private static List<string> GetMissingContactInfo(StateContext ctx)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone")))
            missing.Add("so dien thoai");
        if (string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress")))
            missing.Add("dia chi");
        return missing;
    }

    private static string BuildDraftConfirmation(StateContext ctx, DraftOrder draftOrder)
    {
        var itemSummary = draftOrder.Items.Count == 0
            ? "đơn của chị"
            : string.Join(", ", draftOrder.Items.Select(item =>
                item.Quantity > 1 ? $"{item.ProductName} x{item.Quantity}" : item.ProductName));
        var merchandiseText = $"{draftOrder.MerchandiseTotal:N0}đ";

        var giftName = ctx.GetData<string>("selectedGiftName");
        var lines = new List<string>
        {
            $"Dạ em đã lên đơn nháp {draftOrder.DraftCode} cho {itemSummary} rồi ạ.",
            $"Tạm tính tiền hàng hiện tại là {merchandiseText}. Phí ship và tổng đơn cuối em sẽ kiểm tra lại theo đơn cụ thể trước khi chốt giao hàng cho chị nha."
        };

        if (!string.IsNullOrWhiteSpace(giftName))
        {
            lines.Add($"Quà tặng đang gắn theo dữ liệu nội bộ hiện tại cho đơn này là {giftName} ạ.");
        }

        lines.Add("Bên em sẽ có bạn kiểm tra lại thông tin và chốt giao hàng cho mình nha.");

        if (ctx.GetData<bool?>("currentOrderUsesUpdatedContact") == true &&
            string.Equals(ctx.GetData<string>("pendingContactQuestion"), "ask_save_new_contact", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add("Nếu chị thấy thông tin mới này đúng rồi thì chị có muốn em cập nhật luôn cho các đơn sau không ạ?");
        }

        return string.Join(" ", lines);
    }

    private static bool IsAwaitingFinalSummaryConfirmation(StateContext ctx)
    {
        return ctx.GetData<bool?>("awaitingFinalSummaryConfirmation") == true;
    }

    private async Task<string?> HandlePendingFinalSummaryConfirmationAsync(
        StateContext ctx,
        string message,
        Services.AI.Models.CustomerIntent? intent)
    {
        if (!IsAwaitingFinalSummaryConfirmation(ctx))
        {
            return null;
        }

        if (HasSelectedProduct(ctx) && HasExplicitFinalSummaryConfirmation(message, intent))
        {
            ctx.SetData("awaitingFinalSummaryConfirmation", false);
            ctx.SetData("finalSummaryShownAt", null);
            ctx.SetData("final_price_summary_ready", true);
            return await TryCreateDraftConfirmationAsync(ctx, message);
        }

        if (HasSelectedProduct(ctx) && LooksLikeFinalSummaryClarification(message))
        {
            return await BuildFinalOrderConfirmationReplyAsync(ctx, message, true);
        }

        ctx.SetData("final_price_summary_ready", false);
        return null;
    }

    private async Task<string?> BuildPriceConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        var snapshot = await BuildCommercialFactSnapshotAsync(ctx, product);
        if (snapshot == null)
        {
            return null;
        }

        ctx.SetData("price_confirmed", snapshot.PriceConfirmed);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("inventory_confirmed", snapshot.InventoryConfirmed);

        var productLabel = snapshot.PriceLabel == null
            ? product.Name
            : $"{product.Name} bản {snapshot.PriceLabel}";
        var lines = new List<string>();

        if (snapshot.PriceConfirmed && snapshot.ConfirmedPrice.HasValue)
        {
            lines.Add($"Dạ {productLabel} hiện bên em đang để giá {snapshot.ConfirmedPrice.Value:N0}đ theo dữ liệu nội bộ ạ.");
        }
        else
        {
            lines.Add($"Dạ với {product.Name}, em chưa dám chốt giá chính xác ngay lúc này ạ. Em cần kiểm tra lại đúng phiên bản và dữ liệu runtime hiện tại rồi báo chị chuẩn hơn nha.");
        }

        if (snapshot.GiftConfirmed)
        {
            lines.Add($"Quà tặng đang gắn theo dữ liệu nội bộ hiện tại là {snapshot.GiftName}, còn ưu đãi khác thì em cần kiểm tra lại ở lúc chốt đơn để báo chị chính xác nha.");
        }
        else
        {
            lines.Add("Ưu đãi hiện tại em sẽ kiểm tra lại theo chính sách áp dụng ở lúc chốt đơn để báo chị chính xác nha.");
        }

        lines.Add("Nếu chị muốn em tính luôn tổng tiền tạm tính hoặc hỗ trợ chốt đơn thì chị nhắn em nha.");
        return string.Join(" ", lines);
    }

    private async Task<string?> BuildInventoryConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        var snapshot = await BuildCommercialFactSnapshotAsync(ctx, product);
        if (snapshot == null)
        {
            return null;
        }

        ctx.SetData("inventory_confirmed", snapshot.InventoryConfirmed);

        if (!snapshot.InventoryConfirmed)
        {
            return $"Dạ với {product.Name}, em chưa xác nhận tồn kho chắc ngay lúc này ạ. Để em kiểm tra lại theo phiên bản cụ thể rồi báo chị chính xác nha.";
        }

        if (snapshot.IsInStock == true)
        {
            return $"Dạ với {product.Name} bản {snapshot.PriceLabel}, theo dữ liệu nội bộ hiện tại thì còn khoảng {snapshot.StockQuantity} sản phẩm ạ.";
        }

        return $"Dạ với {product.Name} bản {snapshot.PriceLabel}, theo dữ liệu nội bộ hiện tại thì em đang chưa thấy còn hàng ạ. Nếu chị muốn em kiểm tra phương án khác thì em hỗ trợ tiếp nha.";
    }

    private static bool IsNumberedSuggestionLine(string line)
    {
        return Regex.IsMatch(line, @"^([1-9]|1\d|20)\)", RegexOptions.CultureInvariant);
    }

    private static bool IsRelatedSuggestionSelection(string message)
    {
        return ExtractRelatedSuggestionSelectionNumber(message).HasValue;
    }

    private static int? ExtractRelatedSuggestionSelectionNumber(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = NormalizeForMatching(message);

        // Pattern 1: Có từ khóa rõ ràng (giữ nguyên logic cũ)
        var match = Regex.Match(normalized, @"\b(san pham|mon|lua chon)\s*(so\s*)?(?<number>[1-9]|1\d|20)\b", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            match = Regex.Match(normalized, @"\bchon\s*(san pham|mon|lua chon)?\s*(so\s*)?(?<number>[1-9]|1\d|20)\b", RegexOptions.CultureInvariant);
        }

        if (match.Success && int.TryParse(match.Groups["number"].Value, out var selectedNumber))
        {
            return selectedNumber;
        }

        // Pattern 2: Số đơn thuần (1-20) - chỉ accept nếu message chỉ chứa số
        // Tránh false positive khi khách nói "tôi muốn 1 cái", "địa chỉ số 1"
        match = Regex.Match(normalized, @"^(?<number>[1-9]|1\d|20)$", RegexOptions.CultureInvariant);
        if (match.Success && int.TryParse(match.Groups["number"].Value, out selectedNumber))
        {
            return selectedNumber;
        }

        return null;
    }

    private static bool HasAmbiguousProductReference(string message)
    {
        var normalized = NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!Regex.IsMatch(normalized, @"\b([2-9]|1\d|20)\s+san pham\b", RegexOptions.CultureInvariant))
        {
            return normalized.Contains("san pham do", StringComparison.Ordinal)
                || normalized.Contains("mon do", StringComparison.Ordinal)
                || normalized.Contains("cai kia", StringComparison.Ordinal)
                || normalized.Contains("san pham kia", StringComparison.Ordinal)
                || normalized.Contains("mon kia", StringComparison.Ordinal)
                || normalized.Contains("cai do", StringComparison.Ordinal)
                || normalized.Contains("ship cho cu", StringComparison.Ordinal)
                || normalized.Contains("dia chi cu", StringComparison.Ordinal);
        }

        return true;
    }

    private async Task<string?> BuildAmbiguousProductClarificationReplyAsync(StateContext ctx)
    {
        var recentMessages = GetHistory(ctx).TakeLast(10).ToList();
        var userCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "user");
        var assistantCandidates = await CollectHistoryProductCandidatesAsync(recentMessages, "assistant");
        var candidates = userCandidates
            .Concat(assistantCandidates)
            .GroupBy(x => x.Product.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Take(3)
            .ToList();

        if (candidates.Count <= 1)
        {
            return null;
        }

        var labels = candidates.Select(x => x.Product.Name).ToList();
        var joinedLabels = labels.Count == 2
            ? $"{labels[0]} hay {labels[1]}"
            : string.Join(", ", labels.Take(labels.Count - 1)) + $" hay {labels.Last()}";

        return $"Dạ để em chốt đúng ý chị thì chị giúp em xác nhận mình đang nói tới {joinedLabels} ạ?";
    }

    private async Task<string?> BuildFinalOrderConfirmationReplyAsync(StateContext ctx, string message, bool forceResend = false)
    {
        if (!forceResend && IsAwaitingFinalSummaryConfirmation(ctx))
        {
            return null;
        }

        await RefreshSelectedProductPolicyContextAsync(ctx, message);

        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (selectedCodes.Count == 0)
        {
            return null;
        }

        var products = new List<Product>();
        foreach (var productCode in selectedCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var product = await ProductMappingService.GetActiveProductByCodeAsync(productCode);
            if (product != null)
            {
                products.Add(product);
            }
        }

        if (products.Count == 0)
        {
            return null;
        }

        var quantities = ctx.GetData<Dictionary<string, int>>("selectedProductQuantities")
                         ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var itemLabels = new List<string>();
        decimal merchandiseTotal = 0;

        foreach (var product in products)
        {
            var quantity = quantities.TryGetValue(product.Code, out var selectedQuantity) && selectedQuantity > 0
                ? selectedQuantity
                : 1;
            merchandiseTotal += product.BasePrice * quantity;
            itemLabels.Add(quantity > 1
                ? $"{product.Name} x{quantity}"
                : $"{product.Name} x1");
        }

        var giftName = ctx.GetData<string>("selectedGiftName");
        var phone = ctx.GetData<string>("customerPhone");
        var address = ctx.GetData<string>("shippingAddress");

        ctx.SetData("awaitingFinalSummaryConfirmation", true);
        ctx.SetData("finalSummaryShownAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        ctx.SetData("final_price_summary_ready", true);
        ctx.SetData("price_confirmed", true);
        ctx.SetData("promotion_confirmed", false);
        ctx.SetData("shipping_policy_confirmed", false);
        ctx.SetData("inventory_confirmed", false);

        var lines = new List<string>
        {
            $"Dạ em tóm tắt đơn của chị như này ạ:",
            $"- Sản phẩm: {string.Join(", ", itemLabels)}",
            $"- Tiền hàng tạm tính: {merchandiseTotal:N0}đ",
            "- Phí ship: em cần kiểm tra lại theo đơn cụ thể trước khi chốt",
            "- Tổng đơn cuối: em sẽ báo lại sau khi kiểm tra đủ phí ship và chính sách áp dụng",
            $"- SĐT nhận hàng: {phone}",
            $"- Địa chỉ giao hàng: {address}"
        };

        if (!string.IsNullOrWhiteSpace(giftName))
        {
            lines.Insert(4, $"- Quà tặng theo dữ liệu nội bộ hiện tại: {giftName}");
        }

        lines.Add("Nếu chị đồng ý đơn này thì chị nhắn em kiểu như \"đúng rồi\" hoặc \"chốt đơn giúp chị\" để em lên đơn nháp nha.");
        return string.Join(Environment.NewLine, lines);
    }

    private static bool HasExplicitFinalSummaryConfirmation(string message, Services.AI.Models.CustomerIntent? intent)
    {
        var normalized = NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Contains("thong tin nao", StringComparison.Ordinal)
            || normalized.Contains("bao nhieu", StringComparison.Ordinal)
            || normalized.Contains("gia sao", StringComparison.Ordinal)
            || normalized.Contains("gia bao nhieu", StringComparison.Ordinal)
            || normalized.Contains("phi ship", StringComparison.Ordinal))
        {
            return false;
        }

        var hasExplicitPhrase = normalized.Contains("dung roi", StringComparison.Ordinal)
            || normalized.Contains("chot don", StringComparison.Ordinal)
            || normalized.Contains("len don", StringComparison.Ordinal)
            || normalized.Contains("xac nhan don", StringComparison.Ordinal)
            || normalized.Contains("dong y", StringComparison.Ordinal)
            || normalized.Contains("ok chot", StringComparison.Ordinal)
            || normalized.Contains("oke chot", StringComparison.Ordinal)
            || normalized.Contains("ok em len don", StringComparison.Ordinal)
            || normalized.Contains("oke em len don", StringComparison.Ordinal)
            || normalized.Contains("chot giup chi", StringComparison.Ordinal);

        return hasExplicitPhrase || intent == Services.AI.Models.CustomerIntent.Confirming;
    }

    private static bool LooksLikeFinalSummaryClarification(string message)
    {
        var normalized = NormalizeForMatching(message);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("thong tin nao", StringComparison.Ordinal)
            || normalized.Contains("bao nhieu", StringComparison.Ordinal)
            || normalized.Contains("tong tien", StringComparison.Ordinal)
            || normalized.Contains("phi ship", StringComparison.Ordinal)
            || normalized.Contains("dia chi nao", StringComparison.Ordinal)
            || normalized.Contains("so nao", StringComparison.Ordinal);
    }

    private static bool HasSelectedProduct(StateContext ctx)
    {
        return (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
    }

    private static bool HasPolicyOrComparisonSignal(string normalizedMessage)
    {
        return normalizedMessage.Contains("freeship", StringComparison.Ordinal)
            || normalizedMessage.Contains("free ship", StringComparison.Ordinal)
            || normalizedMessage.Contains("phi ship", StringComparison.Ordinal)
            || normalizedMessage.Contains("van chuyen", StringComparison.Ordinal)
            || normalizedMessage.Contains("khuyen mai", StringComparison.Ordinal)
            || normalizedMessage.Contains("uu dai", StringComparison.Ordinal)
            || normalizedMessage.Contains("giam gia", StringComparison.Ordinal)
            || normalizedMessage.Contains("qua tang", StringComparison.Ordinal)
            || normalizedMessage.Contains("so voi", StringComparison.Ordinal)
            || normalizedMessage.Contains("voi", StringComparison.Ordinal)
            || normalizedMessage.Contains("hay", StringComparison.Ordinal)
            || normalizedMessage.Contains("con", StringComparison.Ordinal)
            || normalizedMessage.Contains("cung", StringComparison.Ordinal)
            || normalizedMessage.Contains("khong em", StringComparison.Ordinal)
            || normalizedMessage.Contains("khong", StringComparison.Ordinal) && !normalizedMessage.Contains("khong lay", StringComparison.Ordinal) && !normalizedMessage.Contains("khong mua", StringComparison.Ordinal);
    }

    private static bool ReferencesProduct(string normalizedMessage, Product product)
    {
        var aliases = GetProductAliases(product);
        return aliases.Any(alias => normalizedMessage.Contains(alias, StringComparison.Ordinal));
    }

    private static bool HasDirectProductCommitment(string normalizedMessage, Product product)
    {
        return GetProductAliases(product).Any(alias =>
            normalizedMessage.Contains($"mua {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"chot {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"len don {alias}", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias} nha", StringComparison.Ordinal)
            || normalizedMessage.Contains($"lay {alias} nhe", StringComparison.Ordinal));
    }

    private static List<string> GetProductAliases(Product product)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizeForMatching(product.Code).Replace("_", " ", StringComparison.Ordinal),
            NormalizeForMatching(product.Name)
        };

        foreach (var token in NormalizeForMatching(product.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            aliases.Add(token);
        }

        return aliases
            .Where(alias => alias.Length >= 2)
            .OrderByDescending(alias => alias.Length)
            .ToList();
    }

    private static string NormalizeForMatching(string input)
    {
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => character
            });
        }

        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }
    private static string GetContactSummary(StateContext ctx)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hasAddress = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;

        return $"SDT={(hasPhone ? (needsConfirmation ? "dang nho lai" : "da co") : "chua co")}, Dia chi={(hasAddress ? (needsConfirmation ? "dang nho lai" : "da co") : "chua co")}";
    }

    /// <summary>
    /// Determines the next conversation state based on AI-detected customer intent.
    /// Replaces brittle if-else logic with intelligent intent-based routing.
    /// </summary>
    private static ConversationState DetermineNextState(
        Services.AI.Models.CustomerIntent intent,
        bool hasProduct,
        bool hasContact)
    {
        return intent switch
        {
            // Customer is browsing - keep them in consulting to help explore options
            Services.AI.Models.CustomerIntent.Browsing => ConversationState.Consulting,

            // Customer needs advice - stay in consulting state
            Services.AI.Models.CustomerIntent.Consulting => ConversationState.Consulting,

            // Customer is ready to buy - keep order flow in collecting info whenever product is known
            Services.AI.Models.CustomerIntent.ReadyToBuy => hasProduct
                ? ConversationState.CollectingInfo
                : ConversationState.Consulting,

            // Customer is confirming info - stay in collecting info
            Services.AI.Models.CustomerIntent.Confirming => ConversationState.CollectingInfo,

            // Customer is asking questions - stay in consulting to answer
            Services.AI.Models.CustomerIntent.Questioning => ConversationState.Consulting,

            // Fallback - use product selection as indicator
            _ => hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting
        };
    }

    private async Task LogMetricsAsync(
        StateContext ctx,
        DateTime startTime,
        int? pipelineLatencyMs,
        string? detectedEmotion,
        decimal? emotionConfidence,
        string? matchedTone,
        string? journeyStage,
        ValidationResult? validationResult)
    {
        try
        {
            var totalResponseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var history = GetHistory(ctx);
            var variant = ctx.GetData<string>("abTestVariant") ?? "control";

            var metricData = new ConversationMetricData
            {
                SessionId = ctx.SessionId,
                FacebookPSID = ctx.FacebookPSID,
                ABTestVariant = variant,
                MessageTimestamp = DateTime.UtcNow,
                ConversationTurn = history.Count,
                TotalResponseTimeMs = totalResponseTime,
                PipelineLatencyMs = pipelineLatencyMs,
                DetectedEmotion = detectedEmotion,
                EmotionConfidence = emotionConfidence,
                MatchedTone = matchedTone,
                JourneyStage = journeyStage,
                ValidationPassed = validationResult?.IsValid,
                ValidationErrors = validationResult?.Issues?.Any() == true
                    ? validationResult.Issues.ToDictionary(e => e.Category, e => (object)e.Message)
                    : null,
                ConversationOutcome = null // Set later when conversation ends
            };

            await ConversationMetricsService.LogAsync(metricData);

            Logger.LogDebug(
                "Metrics logged - PSID: {PSID}, Variant: {Variant}, Latency: {Latency}ms",
                ctx.FacebookPSID,
                variant,
                totalResponseTime);
        }
        catch (Exception ex)
        {
            // Never fail user request due to metrics logging
            Logger.LogError(ex, "Failed to log metrics for PSID: {PSID}", ctx.FacebookPSID);
        }
    }
}
