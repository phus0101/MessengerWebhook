using System.Diagnostics;
using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.AI.Resilience;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.Sales;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Intent;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;
using Microsoft.Extensions.Options;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.StateMachine.Handlers;

public abstract class SalesStateHandlerBase : IStateHandler
{
    protected readonly IGeminiService GeminiService;
    protected readonly IPolicyGuardService PolicyGuardService;
    protected readonly ICaseEscalationService CaseEscalationService;
    protected readonly ICustomerIntelligenceService CustomerIntelligenceService;
    protected readonly DraftOrderCoordinator DraftOrderCoordinator;
    protected readonly IRAGService? RagService;
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
    private readonly ILlmFallbackService _fallbackService;
    private readonly IConversationSummarizer _conversationSummarizer;
    private readonly ICommerceMsgIntentDetector _intentDetector;

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
        IOptions<PolicyGuardOptions> policyGuardOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger logger,
        IProductGroundingService? productGroundingService,
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder,
        IContactConfirmationFlow contactFlow,
        ISalesReplyOrchestrator replyOrchestrator,
        ISalesConsultationReplies consultationReplies,
        ILlmFallbackService llmFallbackService,
        IConversationSummarizer conversationSummarizer,
        ICommerceMsgIntentDetector intentDetector)
    {
        GeminiService = geminiService;
        PolicyGuardService = policyGuardService;
        CaseEscalationService = caseEscalationService;
        CustomerIntelligenceService = customerIntelligenceService;
        DraftOrderCoordinator = draftOrderCoordinator;
        RagService = ragService;
        SubIntentClassifier = subIntentClassifier;
        _productGroundingService = productGroundingService ?? new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector());
        SalesBotOptions = salesBotOptions.Value;
        PolicyGuardOptions = policyGuardOptions.Value;
        RagOptions = ragOptions.Value;
        Logger = logger;
        _contextResolver = contextResolver;
        _promptBuilder = promptBuilder;
        _contactFlow = contactFlow;
        _replyOrchestrator = replyOrchestrator;
        _consultationReplies = consultationReplies;
        _fallbackService = llmFallbackService;
        _conversationSummarizer = conversationSummarizer;
        _intentDetector = intentDetector;
    }

    public async Task<string> HandleAsync(StateContext ctx, string message)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Logger.LogInformation("Handling state {State}", HandledState);
            return await HandleInternalAsync(ctx, message);
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            // LLM circuit is open — return degraded response without changing state
            Logger.LogWarning("LlmCircuit State=Open, returning degraded response for {State}", HandledState);
            return _fallbackService.GetDegradedResponse(ctx.CurrentState);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sales state error in {State}", HandledState);
            ctx.CurrentState = ConversationState.Error;
            return "Dạ em đang bị nghẽn ở hệ thống một chút. Chị nhắn lại giúp em sau ít phút nha.";
        }
        finally
        {
            sw.Stop();
            var historyCount = ConversationHistoryHelper.GetHistory(ctx).Count;
            Logger.LogInformation(
                "SalesHandlerCompleted State={State} ElapsedMs={ElapsedMs} HistoryCount={HistoryCount}",
                HandledState, sw.ElapsedMilliseconds, historyCount);
        }
    }

    protected abstract Task<string> HandleInternalAsync(StateContext ctx, string message);

    protected async Task<string> HandleSalesConversationAsync(StateContext ctx, string message)
    {
        if (SalesBotOptions.SummarizationEnabled)
        {
            await ConversationHistoryHelper.AddToHistoryWithSummaryAsync(
                ctx, "user", message,
                SalesBotOptions.ConversationHistoryLimit,
                SalesBotOptions.EphemeralWindowSize,
                SalesBotOptions.SummarizationThreshold,
                _conversationSummarizer);
        }
        else
        {
            ConversationHistoryHelper.AddToHistory(ctx, "user", message, SalesBotOptions.ConversationHistoryLimit);
        }

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

        // Detect all keyword-based commerce intent signals in one pass
        var keywordIntent = _intentDetector.DetectFromKeywords(message, ctx, hasProduct, hasContact);
        var hasPendingFinalSummaryConfirmation = SalesMessageParser.IsAwaitingFinalSummaryConfirmation(ctx);
        var activeProductsForIntent = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var isRelatedSuggestionSelection = keywordIntent.IsRelatedSuggestionSelection;
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
            intentResult.Intent == CustomerIntent.ReadyToBuy &&
            intentResult.Confidence >= SalesBotOptions.IntentConfidenceThreshold)
        {
            var currentCount = ctx.GetData<int>("consultationRejectionCount");
            ctx.SetData("consultationRejectionCount", currentCount + 1);

            Logger.LogInformation(
                "Consultation rejection detected (count: {Count})",
                currentCount + 1
            );
        }

        // Classify sub-intent for Consulting state (MUST run before question handlers to avoid early returns)
        SubIntentResult? subIntent = null;
        if (intentResult.Confidence >= SalesBotOptions.IntentConfidenceThreshold
            && intentResult.Intent == CustomerIntent.Consulting)
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

        // Merge AI intent into the keyword snapshot — single unified intent struct for the rest of the method
        var msgIntent = await _intentDetector.MergeWithAiIntentAsync(
            keywordIntent, intentResult, subIntent, (float)SalesBotOptions.IntentConfidenceThreshold);

        // Recover product from history when customer is already in ordering flow but product context was lost.
        if (!hasProduct &&
            (hasContact ||
             msgIntent.HasBuySignal ||
             (isRelatedSuggestionSelection && resolvedRelatedSuggestionSelection) ||
             ctx.CurrentState == ConversationState.CollectingInfo ||
             (msgIntent.UseAiIntent &&
              (msgIntent.Intent == CustomerIntent.ReadyToBuy ||
               msgIntent.Intent == CustomerIntent.Confirming))))
        {
            Logger.LogInformation("Ordering flow detected without product in context, attempting to extract from history");
            await _contextResolver.TryExtractProductFromHistoryAsync(ctx, message);
            hasProduct = SalesMessageParser.HasSelectedProduct(ctx);
        }

        if (isRelatedSuggestionSelection && hasProduct)
        {
            var selectedSuggestionReply = await _consultationReplies.TryBuildOfferResponseAsync(ctx, message, CustomerIntent.Browsing);
            if (!string.IsNullOrWhiteSpace(selectedSuggestionReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", selectedSuggestionReply, SalesBotOptions.ConversationHistoryLimit);
                return selectedSuggestionReply;
            }
        }

        var nextState = msgIntent.UseAiIntent
            ? _promptBuilder.DetermineNextState(msgIntent.Intent, hasProduct, hasContact)
            : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);

        if (hasPendingFinalSummaryConfirmation)
        {
            var finalSummaryReply = await HandlePendingFinalSummaryConfirmationAsync(ctx, message, msgIntent.UseAiIntent ? msgIntent.Intent : null);
            if (!string.IsNullOrWhiteSpace(finalSummaryReply))
            {
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", finalSummaryReply, SalesBotOptions.ConversationHistoryLimit);
                return finalSummaryReply;
            }
        }

        if (msgIntent.HasAmbiguousProductReference)
        {
            var ambiguousReferenceReply = await _consultationReplies.BuildAmbiguousProductClarificationReplyAsync(ctx);
            if (!string.IsNullOrWhiteSpace(ambiguousReferenceReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", ambiguousReferenceReply, SalesBotOptions.ConversationHistoryLimit);
                return ambiguousReferenceReply;
            }
        }

        if (msgIntent.IsContactMemoryQuestion)
        {
            var contactMemoryReply = await _contactFlow.BuildContactMemoryReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(contactMemoryReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactMemoryReply, SalesBotOptions.ConversationHistoryLimit);
                return contactMemoryReply;
            }
        }

        if (msgIntent.IsPendingContactClarification)
        {
            var contactClarificationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactClarificationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactClarificationReply, SalesBotOptions.ConversationHistoryLimit);
                return contactClarificationReply;
            }
        }

        if (msgIntent.IsGenericBuyContinuation)
        {
            var contactConfirmationReply = _promptBuilder.BuildPendingContactClarificationReply(ctx);
            if (!string.IsNullOrWhiteSpace(contactConfirmationReply))
            {
                ctx.CurrentState = ConversationState.CollectingInfo;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", contactConfirmationReply, SalesBotOptions.ConversationHistoryLimit);
                return contactConfirmationReply;
            }
        }

        if (msgIntent.HasOrderEstimateQuestion)
        {
            var orderEstimateReply = await _consultationReplies.BuildOrderEstimateReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(orderEstimateReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", orderEstimateReply, SalesBotOptions.ConversationHistoryLimit);
                return orderEstimateReply;
            }
        }

        if (msgIntent.HasProductQuestion || (msgIntent.IsQuestioning && !msgIntent.HasPolicyQuestion && !msgIntent.HasPriceQuestion && !msgIntent.HasInventoryQuestion && !msgIntent.IsContactMemoryQuestion && !msgIntent.IsPendingContactClarification && !msgIntent.HasOrderEstimateQuestion && (msgIntent.HasQuestionMarker || ctx.CurrentState == ConversationState.Consulting)))
        {
            var consultReply = await _consultationReplies.BuildProductConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(consultReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", consultReply, SalesBotOptions.ConversationHistoryLimit);
                return consultReply;
            }
        }

        if (msgIntent.HasPolicyQuestion || (msgIntent.IsQuestioning && msgIntent.HasQuestionMarker && !msgIntent.HasBuySignal && !msgIntent.HasPriceQuestion && !msgIntent.HasInventoryQuestion && !msgIntent.HasOrderEstimateQuestion))
        {
            var shippingReply = await _consultationReplies.BuildShippingConsultationReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(shippingReply))
            {
                ctx.CurrentState = ConversationState.Consulting;
                ConversationHistoryHelper.AddToHistory(ctx, "assistant", shippingReply, SalesBotOptions.ConversationHistoryLimit);
                return shippingReply;
            }
        }

        if (msgIntent.HasInventoryQuestion)
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

        if (msgIntent.HasPriceQuestion)
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

        if (msgIntent.HasBuySignal && hasProduct)
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
                                    (msgIntent.UseAiIntent &&
                                     (msgIntent.Intent == CustomerIntent.ReadyToBuy ||
                                      msgIntent.Intent == CustomerIntent.Confirming)) ||
                                    (ctx.CurrentState == ConversationState.CollectingInfo &&
                                     (!msgIntent.UseAiIntent ||
                                      (msgIntent.Intent != CustomerIntent.Questioning &&
                                       msgIntent.Intent != CustomerIntent.Consulting)))
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
            msgIntent.UseAiIntent &&
            msgIntent.Intent == CustomerIntent.ReadyToBuy &&
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
        if (msgIntent.UseAiIntent && (msgIntent.Intent == CustomerIntent.ReadyToBuy ||
                                      msgIntent.Intent == CustomerIntent.Browsing))
        {
            offerResponse = await _consultationReplies.TryBuildOfferResponseAsync(ctx, message, msgIntent.Intent);
        }

        // Show product offer if available and intent allows it
        if (!string.IsNullOrWhiteSpace(offerResponse))
        {
            ctx.CurrentState = nextState;
            ConversationHistoryHelper.AddToHistory(ctx, "assistant", offerResponse, SalesBotOptions.ConversationHistoryLimit);
            return offerResponse;
        }

        if (msgIntent.RequiresProductGrounding && (!RagOptions.Enabled || RagService == null))
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
            Intent = msgIntent.UseAiIntent ? msgIntent.Intent : null,
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
        CustomerIntent? intent)
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
