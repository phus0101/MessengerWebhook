using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Survey;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.BackgroundServices;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class CompleteStateHandler : SalesStateHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CSATSurveyOptions _surveyOptions;

    public override ConversationState HandledState => ConversationState.Complete;

    public CompleteStateHandler(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        DraftOrderCoordinator draftOrderCoordinator,
        ICustomerIntelligenceService customerIntelligenceService,
        IRAGService? ragService,
        IEmotionDetectionService emotionDetectionService,
        IToneMatchingService toneMatchingService,
        IConversationContextAnalyzer conversationContextAnalyzer,
        ISmallTalkService smallTalkService,
        IResponseValidationService responseValidationService,
        IABTestService abTestService,
        IConversationMetricsService conversationMetricsService,
        ISubIntentClassifier subIntentClassifier,
        IServiceProvider serviceProvider,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        IOptions<CSATSurveyOptions> surveyOptions,
        ILogger<CompleteStateHandler> logger,
        IProductGroundingService? productGroundingService = null)
        : base(
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
            ragOptions,
            logger,
            productGroundingService)
    {
        _serviceProvider = serviceProvider;
        _surveyOptions = surveyOptions.Value;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        var pendingContactQuestion = ctx.GetData<string>("pendingContactQuestion");
        if (string.Equals(pendingContactQuestion, "ask_save_new_contact", StringComparison.OrdinalIgnoreCase))
        {
            var saveReply = await HandleSaveUpdatedContactReplyAsync(ctx, message);
            if (!string.IsNullOrWhiteSpace(saveReply))
            {
                return saveReply;
            }
        }

        // Detect if this is a new conversation
        if (IsNewConversation(ctx, message))
        {
            Logger.LogInformation("New conversation detected after Complete state for PSID: {PSID}, resetting to Consulting", ctx.FacebookPSID);

            // Reset state and clear ALL order-related data
            ctx.CurrentState = ConversationState.Consulting;
            ctx.SetData("draftOrderId", null);
            ctx.SetData("draftOrderCode", null);
            ctx.SetData("selectedProductCodes", null);
            ctx.SetData("selectedProductQuantities", null);
            ctx.SetData("selectedGiftCode", null);
            ctx.SetData("selectedGiftName", null);
            ctx.SetData("shippingFee", null);
            ctx.SetData("customerPhone", null);
            ctx.SetData("shippingAddress", null);
            ctx.SetData("rememberedCustomerPhone", null);
            ctx.SetData("rememberedShippingAddress", null);
            ctx.SetData("contactNeedsConfirmation", false);
            ctx.SetData("contactMemorySource", null);
            ctx.SetData("pendingContactQuestion", null);
            ctx.SetData("currentOrderUsesUpdatedContact", false);
            ctx.SetData("saveCurrentContactForFuture", false);
            ctx.SetData("conversationHistory", new List<MessengerWebhook.Services.AI.Models.ConversationMessage>());
            ctx.SetData("vipGreetingSent", false);
            ctx.SetData("consultationRejectionCount", 0);
            ctx.SetData("consultationDeclined", false);
            ctx.SetData("awaitingFinalSummaryConfirmation", false);
            ctx.SetData("finalSummaryShownAt", null);
            ctx.SetData("final_price_summary_ready", false);
            ctx.SetData("price_confirmed", false);
            ctx.SetData("promotion_confirmed", false);
            ctx.SetData("shipping_policy_confirmed", false);
            ctx.SetData("inventory_confirmed", false);
            ctx.SetData("surveySent", false);

            // Delegate to consulting handler for greeting
            return await HandleSalesConversationAsync(ctx, message);
        }

        // Policy guard check before any draft order follow-up reply
        AddToHistory(ctx, "user", message);
        var history = GetHistory(ctx);
        var policyRequest = new PolicyGuardRequest(
            message,
            ctx.GetData<Guid?>("supportCaseId").HasValue,
            ctx.GetData<Guid?>("draftOrderId").HasValue || !string.IsNullOrWhiteSpace(ctx.GetData<string>("draftOrderCode")),
            history
                .TakeLast(PolicyGuardOptions.MaxRecentTurns)
                .Select(turn => new PolicyConversationTurn(turn.Role, turn.Content))
                .Append(new PolicyConversationTurn("user", message))
                .ToArray(),
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"),
            ctx.CurrentState.ToString(),
            ctx.GetData<string>("knownIntent"),
            (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).ToArray());

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

        // Schedule CSAT survey only when this message still belongs to the completed-order follow-up flow
        if (_surveyOptions.Enabled && !ctx.GetData<bool>("surveySent"))
        {
            var delay = TimeSpan.FromMinutes(_surveyOptions.DelayMinutes);
            CSATSurveySchedulerService.ScheduleSurvey(ctx.SessionId, delay, _serviceProvider, Logger);
            ctx.SetData("surveySent", true);
            Logger.LogInformation("CSAT survey scheduled for session {SessionId} with {Delay}min delay", ctx.SessionId, delay.TotalMinutes);
        }

        // Original behavior for follow-up messages about the order
        ctx.SetData("consultationRejectionCount", 0);
        ctx.SetData("consultationDeclined", false);

        var normalizedMessage = message.Trim().ToLowerInvariant();
        if (normalizedMessage.Contains("thông tin nào", StringComparison.OrdinalIgnoreCase) ||
            normalizedMessage.Contains("thong tin nao", StringComparison.OrdinalIgnoreCase))
        {
            var draftCodeForClarification = ctx.GetData<string>("draftOrderCode");
            var giftName = ctx.GetData<string>("selectedGiftName");
            var giftSegment = string.IsNullOrWhiteSpace(giftName)
                ? string.Empty
                : $", quà tặng {giftName}";
            var clarificationReply = string.IsNullOrWhiteSpace(draftCodeForClarification)
                ? $"Dạ bên em sẽ kiểm tra lại giúp chị các thông tin như sản phẩm đã chốt, số lượng, SĐT, địa chỉ giao hàng, phí ship{giftSegment} trước khi xác nhận ạ."
                : $"Dạ với đơn nháp {draftCodeForClarification}, bên em sẽ kiểm tra lại giúp chị sản phẩm đã chốt, số lượng, SĐT, địa chỉ giao hàng, phí ship{giftSegment} trước khi xác nhận ạ.";
            AddToHistory(ctx, "assistant", clarificationReply);
            return clarificationReply;
        }

        var draftCode = ctx.GetData<string>("draftOrderCode");
        var response = string.IsNullOrWhiteSpace(draftCode)
            ? "Dạ em da len don nhap cho chi roi a. Ben em se kiem tra lai thong tin va lien he xac nhan nha."
            : $"Dạ don nhap {draftCode} cua chi dang cho ben em kiem tra lai thong tin nha.";
        AddToHistory(ctx, "assistant", response);
        return response;
    }

    private async Task<string?> HandleSaveUpdatedContactReplyAsync(Models.StateContext ctx, string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var yesPhrases = new[]
        {
            "co", "có", "co em", "có em", "cap nhat", "cập nhật", "cap nhat giup chi", "cập nhật giúp chị",
            "luu giup chi", "lưu giúp chị", "ok em cap nhat", "ok em cập nhật", "dong y", "đồng ý"
        };
        var noPhrases = new[]
        {
            "khong", "không", "thoi em", "thôi em", "khong can", "không cần", "de vay", "để vậy", "khong nha", "không nha"
        };

        if (noPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            ctx.SetData("saveCurrentContactForFuture", false);
            ctx.SetData("pendingContactQuestion", null);
            ctx.SetData("currentOrderUsesUpdatedContact", false);
            return "Dạ em giữ nguyên thông tin cũ trong hệ thống cho các lần sau nha chị.";
        }

        if (yesPhrases.Any(phrase => normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            await CustomerIntelligenceService.GetOrCreateAsync(
                ctx.FacebookPSID,
                ctx.GetData<string>("facebookPageId"),
                ctx.GetData<string>("customerPhone"),
                ctx.GetData<string>("customerName"),
                ctx.GetData<string>("shippingAddress"));

            ctx.SetData("rememberedCustomerPhone", ctx.GetData<string>("customerPhone"));
            ctx.SetData("rememberedShippingAddress", ctx.GetData<string>("shippingAddress"));
            ctx.SetData("saveCurrentContactForFuture", true);
            ctx.SetData("pendingContactQuestion", null);
            ctx.SetData("currentOrderUsesUpdatedContact", false);
            return "Dạ em cập nhật thông tin mới này vào hồ sơ của chị cho các lần sau rồi ạ.";
        }

        return "Dạ nếu chị muốn em cập nhật thông tin mới này cho các đơn sau thì chị nhắn em " +
               "\"có em cập nhật giúp chị\" nha, còn nếu chưa cần thì chị nhắn em \"không em cứ giữ thông tin cũ\" là được ạ.";
    }

    private bool IsNewConversation(Models.StateContext ctx, string message)
    {
        // Check 1: Greeting-only message starts a fresh conversation, but greeting-prefixed order follow-up should keep current context.
        var normalizedMessage = message.Trim().ToLowerInvariant();
        if (IsGreetingOnlyMessage(normalizedMessage))
        {
            Logger.LogInformation("Greeting-only message detected in Complete state for PSID: {PSID}", ctx.FacebookPSID);
            return true;
        }

        // Check 2: Time since last interaction > 24 hours
        var hoursSinceLastInteraction = (DateTime.UtcNow - ctx.LastInteractionAt).TotalHours;
        if (hoursSinceLastInteraction > 24)
        {
            Logger.LogInformation("Last interaction was {Hours}h ago for PSID: {PSID}, treating as new conversation",
                hoursSinceLastInteraction, ctx.FacebookPSID);
            return true;
        }

        return false;
    }

    private static bool IsGreetingOnlyMessage(string normalizedMessage)
    {
        var sanitized = normalizedMessage.Trim().Trim('!', '?', '.', ',', ';', ':');
        return sanitized switch
        {
            "hi" or "hello" or "chao" or "chào" or "alo" or "alô" or
            "hi em" or "hello em" or "chao em" or "chào em" or "alo em" or "alô em" or
            "hi shop" or "hello shop" or "chao shop" or "chào shop" or "alo shop" or "alô shop"
                => true,
            _ => false
        };
    }
}
