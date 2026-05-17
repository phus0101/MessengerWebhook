using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Resilience;
using MessengerWebhook.Services.Consent;
using MessengerWebhook.Services.Customers;
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
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Intent;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class CollectingInfoStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.CollectingInfo;

    private readonly IConsentService _consentService;
    private readonly ITenantContext _tenantContext;
    private readonly ConsentOptions _consentOptions;

    public CollectingInfoStateHandler(
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
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<PolicyGuardOptions> policyGuardOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger<CollectingInfoStateHandler> logger,
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder,
        IContactConfirmationFlow contactFlow,
        ISalesReplyOrchestrator replyOrchestrator,
        ISalesConsultationReplies consultationReplies,
        ILlmFallbackService llmFallbackService,
        IConversationSummarizer conversationSummarizer,
        ICommerceMsgIntentDetector intentDetector,
        IConsentService consentService,
        ITenantContext tenantContext,
        IOptions<ConsentOptions> consentOptions,
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
            policyGuardOptions,
            ragOptions,
            logger,
            productGroundingService,
            contextResolver,
            promptBuilder,
            contactFlow,
            replyOrchestrator,
            consultationReplies,
            llmFallbackService,
            conversationSummarizer,
            intentDetector)
    {
        _consentService = consentService;
        _tenantContext = tenantContext;
        _consentOptions = consentOptions.Value;
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        // Snapshot PII state before processing so we can detect newly captured info
        var hadPhoneBefore = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hadAddressBefore = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));

        var reply = await HandleSalesConversationAsync(ctx, message);

        // Record implied consent when customer proactively sends new PII in this turn
        await TryRecordImpliedConsentAsync(ctx, hadPhoneBefore, hadAddressBefore);

        return reply;
    }

    /// <summary>
    /// Records implied consent when new PII (phone or address) appeared during this turn.
    /// Path A (Implied): customer voluntarily provided PII → PDPL Art. 11(b) implied basis.
    /// Non-blocking: failures are logged but do not interrupt the conversation flow.
    /// </summary>
    private async Task TryRecordImpliedConsentAsync(
        Models.StateContext ctx,
        bool hadPhoneBefore,
        bool hadAddressBefore)
    {
        var hasPhoneNow = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hasAddressNow = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));

        // Only fire when PII is genuinely new in this turn
        var newPiiCaptured = (!hadPhoneBefore && hasPhoneNow) || (!hadAddressBefore && hasAddressNow);
        if (!newPiiCaptured) return;

        // Requires a resolved tenant — skip in contexts where tenant is not yet known
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue) return;

        try
        {
            // Avoid duplicate records within the same conversation session
            var alreadyRecorded = ctx.GetData<bool?>("consentImpliedRecorded") == true;
            if (alreadyRecorded) return;

            await _consentService.RecordConsentAsync(
                tenantId.Value,
                ctx.FacebookPSID,
                ConsentDecision.Implied,
                "order_fulfillment",
                "messenger",
                _consentOptions.DefaultConsentText);

            ctx.SetData("consentImpliedRecorded", true);
        }
        catch (Exception ex)
        {
            // Consent audit failure must not break the conversation
            Logger.LogError(ex, "Failed to record implied consent for PSID={Psid}", ctx.FacebookPSID);
        }
    }
}
