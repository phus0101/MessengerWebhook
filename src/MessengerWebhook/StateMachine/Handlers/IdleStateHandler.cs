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
using MessengerWebhook.Services.SubIntent;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class IdleStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.Idle;

    public IdleStateHandler(
        IGeminiService geminiService,
        DraftOrderCoordinator draftOrderCoordinator,
        IEmotionDetectionService emotionDetectionService,
        IToneMatchingService toneMatchingService,
        IConversationContextAnalyzer conversationContextAnalyzer,
        ISmallTalkService smallTalkService,
        IResponseValidationService responseValidationService,
        IABTestService abTestService,
        IConversationMetricsService conversationMetricsService,
        ISubIntentClassifier subIntentClassifier,
        ILogger<IdleStateHandler> logger)
        : this(
            geminiService,
            SalesHandlerFallbacks.PolicyGuardService,
            SalesHandlerFallbacks.ProductMappingService,
            SalesHandlerFallbacks.GiftSelectionService,
            SalesHandlerFallbacks.FreeshipCalculator,
            SalesHandlerFallbacks.CaseEscalationService,
            draftOrderCoordinator ?? SalesHandlerFallbacks.DraftOrderCoordinator!,
            SalesHandlerFallbacks.CustomerIntelligenceService,
            null,
            emotionDetectionService,
            toneMatchingService,
            conversationContextAnalyzer,
            smallTalkService,
            responseValidationService,
            abTestService,
            conversationMetricsService,
            subIntentClassifier,
            SalesHandlerFallbacks.Options,
            SalesHandlerFallbacks.RagOptions,
            logger)
    {
    }

    public IdleStateHandler(
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
        IOptions<RAGOptions> ragOptions,
        ILogger<IdleStateHandler> logger,
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
    }

    protected override Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        return HandleSalesConversationAsync(ctx, message);
    }
}
