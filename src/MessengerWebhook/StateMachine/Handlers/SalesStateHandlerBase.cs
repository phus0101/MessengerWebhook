using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using MessengerWebhook.StateMachine.Models;
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
    protected readonly IDraftOrderService DraftOrderService;
    protected readonly ICustomerIntelligenceService CustomerIntelligenceService;
    protected readonly SalesBotOptions SalesBotOptions;
    protected readonly ILogger Logger;

    public abstract ConversationState HandledState { get; }

    protected SalesStateHandlerBase(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        IDraftOrderService draftOrderService,
        ICustomerIntelligenceService customerIntelligenceService,
        IOptions<SalesBotOptions> salesBotOptions,
        ILogger logger)
    {
        GeminiService = geminiService;
        PolicyGuardService = policyGuardService;
        ProductMappingService = productMappingService;
        GiftSelectionService = giftSelectionService;
        FreeshipCalculator = freeshipCalculator;
        CaseEscalationService = caseEscalationService;
        DraftOrderService = draftOrderService;
        CustomerIntelligenceService = customerIntelligenceService;
        SalesBotOptions = salesBotOptions.Value;
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
        var decision = PolicyGuardService.Evaluate(message);
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

        Logger.LogInformation(
            "After CaptureCustomerDetails - PSID: {PSID}, HasProduct: {HasProduct}, HasRequiredContact: {HasRequiredContact}, NeedsConfirmation: {NeedsConfirmation}",
            ctx.FacebookPSID,
            HasSelectedProduct(ctx),
            SalesMessageParser.HasRequiredContact(ctx),
            ctx.GetData<bool?>("contactNeedsConfirmation") ?? false
        );

        // AI Intent Detection - understand customer's true intent BEFORE building offer
        var hasProduct = HasSelectedProduct(ctx);
        var hasContact = SalesMessageParser.HasRequiredContact(ctx);
        var recentHistory = GetHistory(ctx).TakeLast(3).ToList();
        var intentResult = await GeminiService.DetectIntentAsync(
            message,
            ctx.CurrentState,
            hasProduct,
            hasContact,
            recentHistory,
            CancellationToken.None);

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
        var nextState = useAiIntent
            ? DetermineNextState(intentResult.Intent, hasProduct, hasContact)
            : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);

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

            // If has contact info, create order immediately
            if (hasContact)
            {
                var draft = await DraftOrderService.CreateFromContextAsync(ctx);
                ctx.SetData("draftOrderId", draft.Id);
                ctx.SetData("draftOrderCode", draft.DraftCode);
                ctx.CurrentState = ConversationState.Complete;

                var confirmation = BuildDraftConfirmation(draft);
                AddToHistory(ctx, "assistant", confirmation);
                return confirmation;
            }

            // Otherwise, move to collecting info
            ctx.CurrentState = ConversationState.CollectingInfo;
            var missingInfo = GetMissingContactInfo(ctx);
            var missing = string.Join(" và ", missingInfo);
            var autoCloseReply = $"Vậy là mình chốt đơn này luôn nha chị. Chị cho em xin {missing} để em lên đơn ạ.";
            AddToHistory(ctx, "assistant", autoCloseReply);
            return autoCloseReply;
        }

        // Create order only if customer is truly ready (ReadyToBuy intent + has all info + high confidence)
        if (useAiIntent && intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy && hasProduct && hasContact)
        {
            var draft = await DraftOrderService.CreateFromContextAsync(ctx);
            ctx.SetData("draftOrderId", draft.Id);
            ctx.SetData("draftOrderCode", draft.DraftCode);
            ctx.CurrentState = ConversationState.Complete;

            var confirmation = BuildDraftConfirmation(draft);
            AddToHistory(ctx, "assistant", confirmation);
            return confirmation;
        }

        // Build product offer ONLY if intent is ReadyToBuy or Browsing
        string? offerResponse = null;
        if (useAiIntent && (intentResult.Intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                            intentResult.Intent == Services.AI.Models.CustomerIntent.Browsing))
        {
            offerResponse = await TryBuildOfferResponseAsync(ctx, message);
        }

        // Show product offer if available and intent allows it
        if (!string.IsNullOrWhiteSpace(offerResponse))
        {
            ctx.CurrentState = nextState;
            AddToHistory(ctx, "assistant", offerResponse);
            return offerResponse;
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

    private async Task<string?> TryBuildOfferResponseAsync(StateContext ctx, string message)
    {
        var product = await ProductMappingService.GetProductByMessageAsync(message);
        if (product == null)
        {
            return null;
        }

        ctx.SetData("selectedProductCodes", new List<string> { product.Code });
        var gift = await GiftSelectionService.SelectGiftForProductAsync(product.Code);
        var shippingFee = FreeshipCalculator.CalculateShippingFee(new List<string> { product.Code });
        ctx.SetData("selectedGiftCode", gift?.Code ?? string.Empty);
        ctx.SetData("selectedGiftName", gift?.Name ?? string.Empty);
        ctx.SetData("shippingFee", shippingFee);

        // Get VIP profile for natural greeting
        var vipProfile = await GetVipProfileAsync(ctx);
        var lines = new List<string>();

        // Add VIP greeting if applicable
        if (vipProfile != null && vipProfile.IsVip && !string.IsNullOrWhiteSpace(vipProfile.GreetingStyle))
        {
            lines.Add(vipProfile.GreetingStyle);
            lines.Add(string.Empty);
        }

        lines.Add($"Em len thong tin cho {product.Name} roi nha.");
        if (gift != null)
        {
            lines.Add($"Qua tang kem theo: {gift.Name}.");
        }

        lines.Add($"Chinh sach ship: {FreeshipCalculator.GetFreeshipMessage(shippingFee == 0)}");
        lines.Add(string.Empty);
        lines.Add(SalesMessageParser.BuildMissingInfoPrompt(ctx));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null)
    {
        var history = GetHistory(ctx);
        var productCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        var contactSummary = GetContactSummary(ctx);

        // Get VIP profile BEFORE building prompt
        var vipProfile = await GetVipProfileAsync(ctx);
        var vipInstruction = BuildVipInstruction(vipProfile);

        // Build CTA context with intent awareness
        var ctaContext = BuildCtaContext(ctx, intent);

        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
Thong tin da co: {contactSummary}
{vipInstruction}

Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.

{ctaContext}
""";

        var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

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

    private static string BuildVipInstruction(VipProfile? vipProfile)
    {
        if (vipProfile == null || !vipProfile.IsVip || string.IsNullOrWhiteSpace(vipProfile.GreetingStyle))
            return string.Empty;

        return $"""
Khach hang VIP:
- Dung giong dieu than mat, gan gui hon (VD: "chi iu", "chi yeu")
- Mo dau bang: "{vipProfile.GreetingStyle}"
- KHONG doi chinh sach gia, chi doi giong dieu
""";
    }

    private static string BuildCtaContext(StateContext ctx, Services.AI.Models.CustomerIntent? intent = null)
    {
        var hasProduct = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        var consultationDeclined = ctx.GetData<bool?>("consultationDeclined") == true;
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;
        var missingInfo = GetMissingContactInfo(ctx);

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

        // Case 1: Existing customer with data loaded from DB - need confirmation
        // ONLY ask for confirmation if customer shows buying intent (ReadyToBuy or Browsing)
        if (needsConfirmation && missingInfo.Count == 0)
        {
            var phone = ctx.GetData<string>("customerPhone");
            var address = ctx.GetData<string>("shippingAddress");

            // Only push for confirmation if customer is ready to buy or browsing products
            if (intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
                intent == Services.AI.Models.CustomerIntent.Browsing)
            {
                return $"""
CTA Instruction: Customer is returning - their info was loaded from previous orders. Naturally confirm their existing info before creating order. Use friendly tone like:
"Em thay chi da dat hang truoc day roi a. Chi van dung SDT {phone} va dia chi {address} dung khong a?"
or "Chi oi, em thay thong tin cu cua chi la SDT {phone} va dia chi {address}. Chi xac nhan lai giup em nhe."

IMPORTANT: If customer already confirmed (said "dung roi", "ok", "van dung", etc.), DO NOT ask again. Move to creating order.
""";
            }

            // For Consulting/Questioning intent - just acknowledge we have their info, don't push for confirmation yet
            return $"""
CTA Instruction: Customer is returning. We have their previous info (SDT {phone}, dia chi {address}) but they're still in consultation phase.
- If they ask about products, provide consultation naturally
- If they ask about their info, mention we have it on file
- DO NOT push for order confirmation yet - let them lead the conversation
""";
        }

        // Case 2: All info collected and confirmed - ready to create order
        if (missingInfo.Count == 0 && !needsConfirmation)
        {
            return """
CTA Instruction: All information is confirmed. Naturally tell customer you're creating the order now. Use friendly tone like "Da em len don cho chi ngay nhe" or "Em chot don cho chi lien a".
""";
        }

        // Case 3: Has product but missing some contact info - ask for missing pieces
        if (hasProduct)
        {
            var missing = string.Join(" va ", missingInfo);
            return $"""
CTA Instruction: Naturally ask customer to provide missing info ({missing}) to complete the order. Use friendly tone like "Chi gui em {missing} de em len don nha" or "Em can {missing} cua chi de len don a".
""";
        }

        // Case 4: No product selected yet - ask to choose product and provide info
        return """
CTA Instruction: Naturally ask customer to choose a product (Kem Chong Nang, Kem Lua, or combo) and provide contact info. Use friendly tone like "Chi chon san pham va gui thong tin cho em nha".
""";
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

    private static string BuildDraftConfirmation(DraftOrder draftOrder)
    {
        // Use neutral message for all customers - don't expose risk assessment
        // Internal risk tracking remains intact in database
        return $"Dạ em da len don nhap {draftOrder.DraftCode} roi a. Ben em se co ban kiem tra lai thong tin va chot giao hang cho minh nha.";
    }

    private static bool HasSelectedProduct(StateContext ctx)
    {
        return (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
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

            // Customer is ready to buy - move to collecting info if missing data
            Services.AI.Models.CustomerIntent.ReadyToBuy => hasProduct && hasContact
                ? ConversationState.Complete  // Will be handled by order creation logic
                : ConversationState.CollectingInfo,

            // Customer is confirming info - stay in collecting info
            Services.AI.Models.CustomerIntent.Confirming => ConversationState.CollectingInfo,

            // Customer is asking questions - stay in consulting to answer
            Services.AI.Models.CustomerIntent.Questioning => ConversationState.Consulting,

            // Fallback - use product selection as indicator
            _ => hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting
        };
    }
}
