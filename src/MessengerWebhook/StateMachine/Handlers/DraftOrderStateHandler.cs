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
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class DraftOrderStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.DraftOrder;

    public DraftOrderStateHandler(
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
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger<DraftOrderStateHandler> logger)
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
            salesBotOptions,
            ragOptions,
            logger)
    {
    }

    protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        var confirmation = await TryCreateDraftConfirmationAsync(ctx, message);
        if (!string.IsNullOrWhiteSpace(confirmation))
        {
            return confirmation;
        }

        ctx.CurrentState = ConversationState.Complete;
        return "Dạ em đã lên đơn nháp rồi ạ. Bên em sẽ kiểm tra lại sản phẩm, số lượng, SĐT, địa chỉ giao hàng và phí ship trước khi xác nhận cho chị nha.";
    }
}
