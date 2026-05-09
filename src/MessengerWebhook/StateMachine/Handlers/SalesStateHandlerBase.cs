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
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
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

    private readonly ISalesContextResolver _contextResolver;
    private readonly ISalesPromptBuilder _promptBuilder;
    private readonly IContactConfirmationFlow _contactFlow;

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
        IProductGroundingService? productGroundingService = null,
        ISalesContextResolver? contextResolver = null,
        ISalesPromptBuilder? promptBuilder = null,
        IContactConfirmationFlow? contactFlow = null)
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
        _contextResolver = contextResolver ?? new SalesContextResolver(
            customerIntelligenceService, productMappingService, giftSelectionService,
            freeshipCalculator, _productGroundingService, geminiService, logger);
        _promptBuilder = promptBuilder ?? new SalesPromptBuilder();
        _contactFlow = contactFlow ?? new ContactConfirmationFlow(_contextResolver, _promptBuilder);
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

        var wasAwaitingOldContactConfirmation =
            ctx.GetData<bool?>("contactNeedsConfirmation") == true &&
            string.Equals(ctx.GetData<string>("pendingContactQuestion"), "confirm_old_contact", StringComparison.OrdinalIgnoreCase);

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

        var confirmedRememberedContactNow = wasAwaitingOldContactConfirmation &&
                                            ctx.GetData<bool?>("contactNeedsConfirmation") != true;
        if (confirmedRememberedContactNow)
        {
            if (!HasSelectedProduct(ctx))
            {
                Logger.LogInformation("Remembered contact confirmed without product context for PSID: {PSID}, attempting history recovery", ctx.FacebookPSID);
                await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
            }

            string? contactConfirmedReply = null;
            if (SalesMessageParser.HasRequiredContact(ctx))
            {
                contactConfirmedReply = HasSelectedProduct(ctx)
                    ? await BuildFinalOrderConfirmationReplyAsync(ctx, message)
                    : "Dạ em đã dùng thông tin cũ cho đơn lần này rồi ạ, nhưng em chưa rõ chị muốn chốt sản phẩm nào. Chị nhắn lại tên hoặc mã sản phẩm giúp em nha.";
            }
            else
            {
                contactConfirmedReply = await _contactFlow.BuildContactCollectionReplyAsync(ctx, message);
            }

            if (!string.IsNullOrWhiteSpace(contactConfirmedReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactConfirmedReply);
                return contactConfirmedReply;
            }
        }

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
        var isRelatedSuggestionSelection = _contextResolver.IsRelatedSuggestionSelection(message);
        var hasPendingFinalSummaryConfirmation = IsAwaitingFinalSummaryConfirmation(ctx);
        var activeProductsForIntent = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
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
            resolvedRelatedSuggestionSelection = await _contextResolver.TryResolveNumberedSuggestionSelectionAsync(ctx, message) != null;
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

        // Classify sub-intent for Consulting state (MUST run before question handlers to avoid early returns)
        SubIntentResult? subIntent = null;
        if (useAiIntent && intentResult.Intent == Services.AI.Models.CustomerIntent.Consulting)
        {
            subIntent = await SubIntentClassifier.ClassifyAsync(message);

            if (subIntent != null)
            {
                Logger.LogInformation(
                    "SubIntent detected: {Category} (confidence: {Confidence}, source: {Source})",
                    subIntent.Category, subIntent.Confidence, subIntent.Source);

                // Store in context for downstream use
                ctx.SetData("subIntent", subIntent);
            }
        }

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
            await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
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
            ? _promptBuilder.DetermineNextState(intentResult.Intent, hasProduct, hasContact)
            : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);

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
        var isContactMemoryQuestion = _contactFlow.IsContactMemoryQuestion(message);
        var isPendingContactClarification = _contactFlow.IsPendingClarificationQuestion(ctx, message);
        var isGenericPendingContactBuyReply = _contactFlow.IsGenericBuyContinuationPendingConfirmation(ctx, message);
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
            var contactMemoryReply = await _contactFlow.BuildContactMemoryReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactMemoryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactMemoryReply);
                return contactMemoryReply;
            }
        }

        if (isPendingContactClarification)
        {
            var contactClarificationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactClarificationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                AddToHistory(ctx, "assistant", contactClarificationReply);
                return contactClarificationReply;
            }
        }

        if (isGenericPendingContactBuyReply)
        {
            var contactConfirmationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
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

            var fallbackReply = _promptBuilder.BuildProductGroundingFallbackReply();
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

            var fallbackReply = _promptBuilder.BuildProductGroundingFallbackReply();
            ctx.CurrentState = ConversationState.Consulting;
            AddToHistory(ctx, "assistant", fallbackReply);
            return fallbackReply;
        }

        if (hasBuyIntentPhrase && hasProduct)
        {
            var contactReply = await _contactFlow.BuildContactCollectionReplyAsync(ctx, message);
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
        //
        // NOTE: reads contactNeedsConfirmation live (no snapshot). Nothing above this line
        // mutates contactNeedsConfirmation after CaptureCustomerDetailsAsync, so the read
        // is equivalent to the removed `needsConfirmation` snapshot. Keep that invariant if
        // expanding this method.
        var canCreateDraftNow = hasContact &&
                                hasProduct &&
                                ctx.GetData<bool?>("contactNeedsConfirmation") != true &&
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
            if (hasContact && ctx.GetData<bool?>("contactNeedsConfirmation") != true)
            {
                // Extract product from history if not already in context
                if (!hasProduct)
                {
                    Logger.LogInformation("No product in context before creating draft order for PSID: {PSID}, attempting to extract from history", ctx.FacebookPSID);
                    await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
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
            var missingInfo = _promptBuilder.GetMissingContactInfo(ctx);
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
            var activeProductsForFallback = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
            var groundingContext = await _productGroundingService.BuildContextWithRelatedSuggestionsAsync(
                message,
                activeProductsForFallback,
                Array.Empty<GroundedProduct>());

            var fallbackReply = groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts
                ? await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null)
                : _promptBuilder.BuildProductGroundingFallbackReply();

            ctx.CurrentState = ConversationState.Consulting;
            AddToHistory(ctx, "assistant", fallbackReply);
            return fallbackReply;
        }

        // Continue conversation based on detected intent

        ctx.CurrentState = nextState;
        var reply = await BuildNaturalReplyAsync(ctx, message, useAiIntent ? intentResult.Intent : null, subIntent);
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

    private async Task<string?> TryBuildOfferResponseAsync(
        StateContext ctx,
        string message,
        Services.AI.Models.CustomerIntent intent)
    {
        Logger.LogInformation(
            "TryBuildOfferResponseAsync called for PSID: {PSID}, Intent: {Intent}, MessageLength={MessageLength}",
            ctx.FacebookPSID, intent, message.Length);

        var product = await _contextResolver.ResolveCurrentProductAsync(ctx, message);
        if (product == null)
        {
            Logger.LogWarning(
                "No product found for PSID: {PSID}, returning null",
                ctx.FacebookPSID);
            return null;
        }

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

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

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
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

    private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null, SubIntentResult? subIntent = null)
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
        var activeProducts = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = _promptBuilder.GetContactSummary(ctx);

        // Get SubIntent from context if available
        var contextSubIntent = ctx.GetData<SubIntentResult?>("subIntent");
        var includeDetailedInfo = contextSubIntent?.Category == SubIntentCategory.ProductQuestion;

        var earlyRagContext = await RetrieveRagContextAsync(ctx, message, includeDetailedInfo);
        var groundingContext = await _productGroundingService.BuildContextWithRelatedSuggestionsAsync(message, activeProducts, earlyRagContext.Products);
        if (groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts)
        {
            return await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);
        }

        history = _productGroundingService.SanitizeAssistantHistory(history, groundingContext.AllowedProducts).ToList();

        // Get VIP profile BEFORE building prompt
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;

        // Get returning customer flag from context
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;

        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = _promptBuilder.BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
            Logger.LogInformation("First greeting sent for PSID: {PSID}", ctx.FacebookPSID);
        }

        // Build CTA context with intent awareness
        var ctaContext = _promptBuilder.BuildCtaContext(ctx, intent);

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
                    var suggestedValidationContext = _promptBuilder.BuildFactValidationContext(
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
                        return _promptBuilder.BuildProductGroundingFallbackReply();
                    }

                    AddToHistory(ctx, "assistant", smallTalkResponse.SuggestedResponse);
                    return smallTalkResponse.SuggestedResponse;
                }
            }
        }

        // Build SubIntent guidance text
        string? subIntentGuidance = null;
        if (subIntent != null)
        {
            subIntentGuidance = subIntent.Category switch
            {
                SubIntentCategory.ProductQuestion =>
                    "Khách hỏi chi tiết về sản phẩm. Hãy cung cấp thông tin đầy đủ về thành phần, công dụng, cách dùng.",
                SubIntentCategory.PriceQuestion =>
                    "Khách hỏi về giá. Hãy giải thích rõ giá, chương trình khuyến mãi, so sánh giá trị.",
                SubIntentCategory.ShippingQuestion =>
                    "Khách hỏi về vận chuyển. Hãy giải thích chính sách ship, thời gian giao hàng.",
                SubIntentCategory.AvailabilityQuestion =>
                    "Khách hỏi về tình trạng hàng. Hãy thông báo còn hàng hay hết, dự kiến nhập hàng.",
                SubIntentCategory.PolicyQuestion =>
                    "Khách hỏi về chính sách. Hãy giải thích chính sách đổi trả, bảo hành.",
                SubIntentCategory.ComparisonQuestion =>
                    "Khách muốn so sánh sản phẩm. Hãy so sánh ưu nhược điểm, phù hợp với nhu cầu nào.",
                _ => null
            };
        }

        var ragContext = string.IsNullOrWhiteSpace(earlyRagContext.FormattedContext)
            ? null
            : earlyRagContext.FormattedContext;

        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
San pham duoc phep neu can neu ten: {_promptBuilder.FormatAllowedProductNames(groundingContext.AllowedProducts)}
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
            ragContext: ragContext,
            subIntentGuidance: subIntentGuidance);

        // Capture pipeline latency
        var pipelineLatency = (int)(DateTime.UtcNow - pipelineStartTime).TotalMilliseconds;

        // Validate response quality before sending to customer
        var validationContext = _promptBuilder.BuildFactValidationContext(
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
            return _promptBuilder.BuildProductGroundingFallbackReply();
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

        var validationContext = _promptBuilder.BuildFactValidationContext(
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

    private async Task<RAGContext> RetrieveRagContextAsync(StateContext ctx, string message, bool includeDetailedInfo = false)
    {
        if (!RagOptions.Enabled || RagService == null)
        {
            return new RAGContext(string.Empty, new List<string>(), new List<GroundedProduct>(), new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "disabled"));
        }

        try
        {
            var ragResult = await RagService.RetrieveContextAsync(message, topK: RagOptions.TopK, includeDetailedInfo);

            Logger.LogInformation(
                "RAG retrieved {Count} products in {Ms}ms for PSID: {PSID} (detailed: {Detailed})",
                ragResult.ProductIds.Count,
                ragResult.Metrics.TotalLatency.TotalMilliseconds,
                ctx.FacebookPSID,
                includeDetailedInfo);

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
        var activeProducts = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = _promptBuilder.GetContactSummary(ctx);

        // Get VIP profile for customer instruction only
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = _promptBuilder.BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
        }

        // Build CTA context
        var ctaContext = _promptBuilder.BuildCtaContext(ctx, intent);

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
San pham duoc phep neu can neu ten: {_promptBuilder.FormatAllowedProductNames(groundingContext.AllowedProducts)}
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

        var validationContext = _promptBuilder.BuildFactValidationContext(
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
            return _promptBuilder.BuildProductGroundingFallbackReply();
        }

        return response;
    }

    private async Task<string?> BuildProductConsultationReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        ctx.SetData("inventory_confirmed", false);

        var lines = new List<string>
        {
            $"Dạ {product.Name} bên em là {_promptBuilder.NormalizeSentence(product.Description)}"
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
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        var effectiveProductCode = product?.Code ?? lockedProductCode;
        if (string.IsNullOrWhiteSpace(effectiveProductCode) || product == null)
        {
            return null;
        }

        var productCodes = new List<string> { effectiveProductCode };
        ctx.SetData("selectedProductCodes", productCodes);
        await _contextResolver.SyncActiveProductPolicyContextAsync(ctx, effectiveProductCode);

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotForPolicyAsync(ctx, product);
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

    private async Task<string?> BuildOrderEstimateReplyAsync(StateContext ctx, string message)
    {
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

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

    protected async Task<string?> TryCreateDraftConfirmationAsync(StateContext ctx, string message)
    {
        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

        var draft = await DraftOrderCoordinator.FinalizeDraftOrderAsync(ctx);
        if (draft == null)
        {
            return null;
        }

        ctx.CurrentState = ConversationState.Complete;
        return _promptBuilder.BuildDraftConfirmation(ctx, draft);
    }

    private async Task<string> BuildFirstGreetingReplyAsync(StateContext ctx)
    {
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var customerName = ctx.GetData<string>("customerName") ?? ctx.GetData<string>("rememberedCustomerName");
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);

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
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotAsync(ctx, product);
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
        var product = await _contextResolver.GetActiveProductOrResolveAsync(ctx, message);
        if (product == null)
        {
            return null;
        }

        var snapshot = await _contextResolver.BuildCommercialFactSnapshotAsync(ctx, product);
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

    private static bool HasAmbiguousProductReference(string message)
    {
        var normalized = SalesTextHelper.NormalizeForMatching(message);
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
        var userCandidates = await _contextResolver.CollectHistoryProductCandidatesAsync(recentMessages, "user");
        var assistantCandidates = await _contextResolver.CollectHistoryProductCandidatesAsync(recentMessages, "assistant");
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

        await _contextResolver.RefreshSelectedProductPolicyContextAsync(ctx, message);

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
        var normalized = SalesTextHelper.NormalizeForMatching(message);
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
        var normalized = SalesTextHelper.NormalizeForMatching(message);
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
