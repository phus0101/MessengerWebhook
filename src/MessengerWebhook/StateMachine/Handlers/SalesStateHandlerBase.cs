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
using MessengerWebhook.Services.Sales;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ISalesReplyOrchestrator _replyOrchestrator;
    private readonly ISalesConsultationReplies _consultationReplies;

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
        IContactConfirmationFlow? contactFlow = null,
        ISalesReplyOrchestrator? replyOrchestrator = null,
        ISalesConsultationReplies? consultationReplies = null)
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
        _replyOrchestrator = replyOrchestrator ?? new SalesReplyOrchestrator(
            geminiService,
            ragService,
            emotionDetectionService,
            toneMatchingService,
            conversationContextAnalyzer,
            smallTalkService,
            responseValidationService,
            abTestService,
            conversationMetricsService,
            customerIntelligenceService,
            _productGroundingService,
            _contextResolver,
            _promptBuilder,
            salesBotOptions,
            ragOptions,
            logger);
        _consultationReplies = consultationReplies ?? new SalesConsultationReplies(
            _contextResolver, _promptBuilder, productMappingService, NullLogger<SalesConsultationReplies>.Instance);
    }

    public async Task<string> HandleAsync(StateContext ctx, string message)
    {
        try
        {
            Logger.LogInformation("Handling state {State}", HandledState);
            return await HandleInternalAsync(ctx, message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sales state error in {State}", HandledState);
            ctx.CurrentState = ConversationState.Error;
            return "Dạ em đang bị nghẽn ở hệ thống một chút. Chị nhắn lại giúp em sau ít phút nha.";
        }
    }

    protected abstract Task<string> HandleInternalAsync(StateContext ctx, string message);

    protected async Task<string> HandleSalesConversationAsync(StateContext ctx, string message)
    {
        ConversationHistoryHelper.AddToHistory(ctx, "user", message, SalesBotOptions.ConversationHistoryLimit);

        // Load remembered contact from previous orders on first message
        var history = ConversationHistoryHelper.GetHistory(ctx);
        Logger.LogInformation("History count: {Count}", history.Count);

        if (history.Count <= 1) // First message in conversation
        {
            var pageId = ctx.GetData<string>("facebookPageId");
            Logger.LogInformation("Attempting to load customer for PageId: {PageId}", pageId);

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
                    Logger.LogInformation("Loaded remembered phone: {Phone}", PiiRedaction.MaskPhone(customer.PhoneNumber ?? string.Empty));
                }

                if (!string.IsNullOrWhiteSpace(customer.ShippingAddress))
                {
                    ctx.SetData("rememberedShippingAddress", customer.ShippingAddress);
                    ctx.SetData("shippingAddress", customer.ShippingAddress);
                    Logger.LogInformation("Loaded remembered address: {Address}", PiiRedaction.MaskAddress(customer.ShippingAddress ?? string.Empty));
                }

                // Set flag to ask for confirmation
                if (!string.IsNullOrWhiteSpace(customer.PhoneNumber) || !string.IsNullOrWhiteSpace(customer.ShippingAddress))
                {
                    ctx.SetData("contactNeedsConfirmation", true);
                    ctx.SetData("contactMemorySource", "previous-order");
                    ctx.SetData("pendingContactQuestion", "confirm_old_contact");
                    Logger.LogInformation("Set contactNeedsConfirmation=true");
                }
            }
        }

        var policyRequest = BuildPolicyGuardRequest(ctx, message, history);
        var decision = await PolicyGuardService.EvaluateAsync(policyRequest);
        if (decision.Action == PolicyAction.SafeReply)
        {
            var safeReply = PolicyGuardOptions.SafeReplyMessage;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", safeReply, SalesBotOptions.ConversationHistoryLimit);
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
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", handoffResponse, SalesBotOptions.ConversationHistoryLimit);
            return handoffResponse;
        }

        var wasAwaitingOldContactConfirmation =
            ctx.GetData<bool?>("contactNeedsConfirmation") == true &&
            string.Equals(ctx.GetData<string>("pendingContactQuestion"), "confirm_old_contact", StringComparison.OrdinalIgnoreCase);

        // Capture customer details first (phone, address, etc.)
        await SalesMessageParser.CaptureCustomerDetailsAsync(ctx, message, GeminiService, Logger);
        SalesMessageParser.CaptureSelectedProductQuantity(ctx, message);

        Logger.LogInformation(
            "After CaptureCustomerDetails HasProduct={HasProduct} HasRequiredContact={HasRequiredContact} NeedsConfirmation={NeedsConfirmation}",
            SalesMessageParser.HasSelectedProduct(ctx),
            SalesMessageParser.HasRequiredContact(ctx),
            ctx.GetData<bool?>("contactNeedsConfirmation") ?? false
        );

        var confirmedRememberedContactNow = wasAwaitingOldContactConfirmation &&
                                            ctx.GetData<bool?>("contactNeedsConfirmation") != true;
        if (confirmedRememberedContactNow)
        {
            if (!SalesMessageParser.HasSelectedProduct(ctx))
            {
                Logger.LogInformation("Remembered contact confirmed without product context, attempting history recovery");
                await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
            }

            string? contactConfirmedReply = null;
            if (SalesMessageParser.HasRequiredContact(ctx))
            {
                contactConfirmedReply = SalesMessageParser.HasSelectedProduct(ctx)
                    ? await _consultationReplies.BuildFinalOrderConfirmationReplyAsync(ctx, message)
                    : "Dạ em đã dùng thông tin cũ cho đơn lần này rồi ạ, nhưng em chưa rõ chị muốn chốt sản phẩm nào. Chị nhắn lại tên hoặc mã sản phẩm giúp em nha.";
            }
            else
            {
                contactConfirmedReply = await _contactFlow.BuildContactCollectionReplyAsync(ctx, message);
            }

            if (!string.IsNullOrWhiteSpace(contactConfirmedReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactConfirmedReply, SalesBotOptions.ConversationHistoryLimit);
                return contactConfirmedReply;
            }
        }

        if (history.Count <= 1 && SalesMessageParser.IsPureGreeting(message) && !SalesMessageParser.HasSelectedProduct(ctx))
        {
            var greetingReply = await _consultationReplies.BuildFirstGreetingReplyAsync(ctx);
            ctx.CurrentState = ConversationState.Consulting;
            ctx.SetData("vipGreetingSent", true);
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", greetingReply, SalesBotOptions.ConversationHistoryLimit);
            return greetingReply;
        }

        // AI Intent Detection - understand customer's true intent BEFORE building offer
        var hasProduct = SalesMessageParser.HasSelectedProduct(ctx);
        var hasContact = SalesMessageParser.HasRequiredContact(ctx);
        var hasBuyIntentPhrase = SalesMessageParser.ContainsAnyPhrase(message,
            "lên đơn", "len don", "chốt đơn", "chot don", "chốt nhé", "chot nhe", "chốt nha", "chot nha",
            "mua luôn", "mua luon", "đặt hàng", "dat hang", "ok em", "oke em", "ok e",
            "lấy sản phẩm này", "lay san pham nay", "lấy nhé", "lay nhe", "lấy nha", "lay nha");
        var isRelatedSuggestionSelection = _contextResolver.IsRelatedSuggestionSelection(message);
        var hasPendingFinalSummaryConfirmation = SalesMessageParser.IsAwaitingFinalSummaryConfirmation(ctx);
        var activeProductsForIntent = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var intentGroundingContext = _productGroundingService.BuildContext(message, activeProductsForIntent, Array.Empty<GroundedProduct>());
        var recentHistory = _productGroundingService
            .SanitizeAssistantHistory(ConversationHistoryHelper.GetHistory(ctx).TakeLast(3), intentGroundingContext.AllowedProducts)
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
            hasProduct = SalesMessageParser.HasSelectedProduct(ctx);
        }

        Logger.LogInformation(
            "AI Intent Detection Intent={Intent} Confidence={Confidence} Method={Method}",
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
                "Consultation rejection detected (count: {Count})",
                currentCount + 1
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
            Logger.LogInformation("Ordering flow detected without product in context, attempting to extract from history");
            await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
            hasProduct = SalesMessageParser.HasSelectedProduct(ctx);
        }

        if (isRelatedSuggestionSelection && hasProduct)
        {
            var selectedSuggestionReply = await _consultationReplies.TryBuildOfferResponseAsync(ctx, message, Services.AI.Models.CustomerIntent.Browsing);
            if (!string.IsNullOrWhiteSpace(selectedSuggestionReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", selectedSuggestionReply, SalesBotOptions.ConversationHistoryLimit);
                return selectedSuggestionReply;
            }
        }

        var nextState = useAiIntent
            ? _promptBuilder.DetermineNextState(intentResult.Intent, hasProduct, hasContact)
            : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);

        var isQuestioning = useAiIntent && intentResult.Intent == Services.AI.Models.CustomerIntent.Questioning;
        var isProductQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "nói thêm", "noi them", "nói kỹ", "noi ky", "chi tiet", "thành phần", "thanh phan", "công dụng", "cong dung",
            "cách dùng", "cach dung", "phù hợp", "phu hop", "dùng sao", "dung sao");
        var isShippingQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "freeship", "free ship", "phí ship", "phi ship", "vận chuyển", "van chuyen", "ship");
        var isPolicyQuestion = isShippingQuestion || SalesMessageParser.ContainsAnyPhrase(message,
            "quà gì", "qua gi", "quà tặng", "qua tang", "tặng gì", "tang gi",
            "khuyến mãi", "khuyen mai", "ưu đãi", "uu dai", "giảm giá", "giam gia", "promo");
        var isPriceQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "giá bao nhiêu", "gia bao nhieu", "giá sao", "gia sao", "bao nhiêu tiền", "bao nhieu tien", "giá", "gia");
        var isInventoryQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "còn hàng", "con hang", "hết hàng", "het hang", "còn không", "con khong", "hết chưa", "het chua",
            "sẵn hàng", "san hang", "có sẵn", "co san", "out stock", "in stock", "tồn kho", "ton kho");
        var isContactMemoryQuestion = _contactFlow.IsContactMemoryQuestion(message);
        var isPendingContactClarification = _contactFlow.IsPendingClarificationQuestion(ctx, message);
        var isGenericPendingContactBuyReply = _contactFlow.IsGenericBuyContinuationPendingConfirmation(ctx, message);
        var isOrderEstimateQuestion = SalesMessageParser.ContainsAnyPhrase(message,
            "tổng tiền", "tong tien", "tổng cộng", "tong cong", "bao nhiêu sản phẩm", "bao nhieu san pham", "bao nhiêu món", "bao nhieu mon");
        var isAmbiguousProductReference = SalesMessageParser.HasAmbiguousProductReference(message);
        var hasQuestionMarker = message.Contains('?');
        var requiresProductGrounding = SalesMessageParser.RequiresProductGrounding(
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
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", finalSummaryReply, SalesBotOptions.ConversationHistoryLimit);
                return finalSummaryReply;
            }
        }

        if (isAmbiguousProductReference)
        {
            var ambiguousReferenceReply = await _consultationReplies.BuildAmbiguousProductClarificationReplyAsync(ctx);
            if (!string.IsNullOrWhiteSpace(ambiguousReferenceReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", ambiguousReferenceReply, SalesBotOptions.ConversationHistoryLimit);
                return ambiguousReferenceReply;
            }
        }

        if (isContactMemoryQuestion)
        {
            var contactMemoryReply = await _contactFlow.BuildContactMemoryReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactMemoryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactMemoryReply, SalesBotOptions.ConversationHistoryLimit);
                return contactMemoryReply;
            }
        }

        if (isPendingContactClarification)
        {
            var contactClarificationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactClarificationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactClarificationReply, SalesBotOptions.ConversationHistoryLimit);
                return contactClarificationReply;
            }
        }

        if (isGenericPendingContactBuyReply)
        {
            var contactConfirmationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactConfirmationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactConfirmationReply, SalesBotOptions.ConversationHistoryLimit);
                return contactConfirmationReply;
            }
        }

        if (isOrderEstimateQuestion)
        {
            var orderEstimateReply = await _consultationReplies.BuildOrderEstimateReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(orderEstimateReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", orderEstimateReply, SalesBotOptions.ConversationHistoryLimit);
                return orderEstimateReply;
            }
        }

        if (isProductQuestion || (isQuestioning && !isPolicyQuestion && !isPriceQuestion && !isInventoryQuestion && !isContactMemoryQuestion && !isPendingContactClarification && !isOrderEstimateQuestion && (hasQuestionMarker || ctx.CurrentState == ConversationState.Consulting)))
        {
            var consultReply = await _consultationReplies.BuildProductConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(consultReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", consultReply, SalesBotOptions.ConversationHistoryLimit);
                return consultReply;
            }
        }

        if (isPolicyQuestion || (isQuestioning && hasQuestionMarker && hasBuyIntentPhrase == false && !isPriceQuestion && !isInventoryQuestion && !isOrderEstimateQuestion))
        {
            var shippingReply = await _consultationReplies.BuildShippingConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(shippingReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", shippingReply, SalesBotOptions.ConversationHistoryLimit);
                return shippingReply;
            }
        }

        if (isInventoryQuestion)
        {
            var inventoryReply = await _consultationReplies.BuildInventoryConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(inventoryReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", inventoryReply, SalesBotOptions.ConversationHistoryLimit);
                return inventoryReply;
            }

            var fallbackReply = _promptBuilder.BuildProductGroundingFallbackReply();
            ctx.CurrentState = ConversationState.Consulting;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", fallbackReply, SalesBotOptions.ConversationHistoryLimit);
            return fallbackReply;
        }

        if (isPriceQuestion)
        {
            var priceReply = await _consultationReplies.BuildPriceConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(priceReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", priceReply, SalesBotOptions.ConversationHistoryLimit);
                return priceReply;
            }

            var fallbackReply = _promptBuilder.BuildProductGroundingFallbackReply();
            ctx.CurrentState = ConversationState.Consulting;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", fallbackReply, SalesBotOptions.ConversationHistoryLimit);
            return fallbackReply;
        }

        if (hasBuyIntentPhrase && hasProduct)
        {
            var contactReply = await _contactFlow.BuildContactCollectionReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactReply, SalesBotOptions.ConversationHistoryLimit);
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
            Logger.LogInformation("Customer has all required info and explicit order intent, building final confirmation summary");

            var finalSummaryReply = await _consultationReplies.BuildFinalOrderConfirmationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(finalSummaryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", finalSummaryReply, SalesBotOptions.ConversationHistoryLimit);
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
                "Auto-closing after {Count} consultation rejections",
                rejectionCount);

            ctx.SetData("consultationDeclined", true);

            // If has contact info, create order immediately (only if confirmed)
            if (hasContact && ctx.GetData<bool?>("contactNeedsConfirmation") != true)
            {
                // Extract product from history if not already in context
                if (!hasProduct)
                {
                    Logger.LogInformation("No product in context before creating draft order, attempting to extract from history");
                    await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
                    hasProduct = SalesMessageParser.HasSelectedProduct(ctx);
                }

                if (!hasProduct)
                {
                    Logger.LogWarning("Cannot create draft order without product");
                    var noProductReply = "Dạ em chưa rõ chị muốn đặt sản phẩm nào ạ. Chị cho em biết tên sản phẩm để em lên đơn nhé.";
                    ConversationHistoryHelper.AddToHistory(ctx, "assistant", noProductReply, SalesBotOptions.ConversationHistoryLimit);
                    return noProductReply;
                }

                var finalSummaryReply = await _consultationReplies.BuildFinalOrderConfirmationReplyAsync(ctx, message);
                if (!string.IsNullOrWhiteSpace(finalSummaryReply))
                {
                    ctx.CurrentState = ConversationState.CollectingInfo;
                    ConversationHistoryHelper.AddToHistory(ctx, "assistant", finalSummaryReply, SalesBotOptions.ConversationHistoryLimit);
                    return finalSummaryReply;
                }
            }

            // Otherwise, move to collecting info
            ctx.CurrentState = ConversationState.CollectingInfo;
            var missingInfo = _promptBuilder.GetMissingContactInfo(ctx);
            var missing = string.Join(" và ", missingInfo);
            var autoCloseReply = $"Vậy là mình chốt đơn này luôn nha chị. Chị cho em xin {missing} để em lên đơn ạ.";
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", autoCloseReply, SalesBotOptions.ConversationHistoryLimit);
            return autoCloseReply;
        }

        // Build product offer only when customer is browsing or explicitly wants to buy.
        string? offerResponse = null;
        if (useAiIntent && (intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                            intentResult.Intent == Services.AI.Models.CustomerIntent.Browsing))
        {
            offerResponse = await _consultationReplies.TryBuildOfferResponseAsync(ctx, message, intentResult.Intent);
        }

        // Show product offer if available and intent allows it
        if (!string.IsNullOrWhiteSpace(offerResponse))
        {
            ctx.CurrentState = nextState;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", offerResponse, SalesBotOptions.ConversationHistoryLimit);
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
                ? await _replyOrchestrator.BuildGroundedFallbackAsync(ctx, message, groundingContext)
                : _promptBuilder.BuildProductGroundingFallbackReply();

            ctx.CurrentState = ConversationState.Consulting;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", fallbackReply, SalesBotOptions.ConversationHistoryLimit);
            return fallbackReply;
        }

        // Continue conversation based on detected intent

        ctx.CurrentState = nextState;
        var reply = await _replyOrchestrator.GenerateAsync(new SalesReplyRequest
        {
            Context = ctx,
            Message = message,
            Intent = useAiIntent ? intentResult.Intent : null,
            SubIntent = subIntent
        });
        ConversationHistoryHelper.AddToHistory(ctx, "assistant", reply, SalesBotOptions.ConversationHistoryLimit);
        return reply;
    }

    protected string BuildHumanHandoffReply()
    {
        return SalesBotOptions.UnsupportedFallbackMessage;
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

    private async Task<string?> HandlePendingFinalSummaryConfirmationAsync(
        StateContext ctx,
        string message,
        Services.AI.Models.CustomerIntent? intent)
    {
        if (!SalesMessageParser.IsAwaitingFinalSummaryConfirmation(ctx))
            return null;

        if (SalesMessageParser.HasSelectedProduct(ctx) && SalesMessageParser.HasExplicitFinalSummaryConfirmation(message, intent))
        {
            ctx.SetData("awaitingFinalSummaryConfirmation", false);
            ctx.SetData("finalSummaryShownAt", null);
            ctx.SetData("final_price_summary_ready", true);
            return await TryCreateDraftConfirmationAsync(ctx, message);
        }

        if (SalesMessageParser.HasSelectedProduct(ctx) && SalesMessageParser.LooksLikeFinalSummaryClarification(message))
            return await _consultationReplies.BuildFinalOrderConfirmationReplyAsync(ctx, message, true);

        ctx.SetData("final_price_summary_ready", false);
        return null;
    }

}
