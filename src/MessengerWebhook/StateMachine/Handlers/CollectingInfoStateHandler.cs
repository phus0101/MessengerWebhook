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
using MessengerWebhook.Services.RAG;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class CollectingInfoStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.CollectingInfo;

    public CollectingInfoStateHandler(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        IDraftOrderService draftOrderService,
        ICustomerIntelligenceService customerIntelligenceService,
        IRAGService? ragService,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger<CollectingInfoStateHandler> logger)
        : base(
            geminiService,
            policyGuardService,
            productMappingService,
            giftSelectionService,
            freeshipCalculator,
            caseEscalationService,
            draftOrderService,
            customerIntelligenceService,
            ragService,
            salesBotOptions,
            ragOptions,
            logger)
    {
    }

    protected override Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        return HandleSalesConversationAsync(ctx, message);
    }
}
