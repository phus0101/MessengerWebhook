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
        IDraftOrderService draftOrderService,
        ICustomerIntelligenceService customerIntelligenceService,
        IRAGService? ragService,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger<HumanHandoffStateHandler> logger)
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
        return Task.FromResult(BuildHumanHandoffReply());
    }
}
