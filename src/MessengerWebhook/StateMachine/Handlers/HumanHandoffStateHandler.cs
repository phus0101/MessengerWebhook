using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Resilience;
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
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Intent;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class HumanHandoffStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.HumanHandoff;

    public HumanHandoffStateHandler(
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
        ILogger<HumanHandoffStateHandler> logger,
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder,
        IContactConfirmationFlow contactFlow,
        ISalesReplyOrchestrator replyOrchestrator,
        ISalesConsultationReplies consultationReplies,
        ILlmFallbackService llmFallbackService,
        IConversationSummarizer conversationSummarizer,
        ICommerceMsgIntentDetector intentDetector,
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
    }

    protected override Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        return Task.FromResult(BuildHumanHandoffReply());
    }
}
