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
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

public class ConsultingStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.Consulting;

    public ConsultingStateHandler(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        IDraftOrderService draftOrderService,
        ICustomerIntelligenceService customerIntelligenceService,
        IOptions<SalesBotOptions> salesBotOptions,
        ILogger<ConsultingStateHandler> logger)
        : base(
            geminiService,
            policyGuardService,
            productMappingService,
            giftSelectionService,
            freeshipCalculator,
            caseEscalationService,
            draftOrderService,
            customerIntelligenceService,
            salesBotOptions,
            logger)
    {
    }

    protected override Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
    {
        return HandleSalesConversationAsync(ctx, message);
    }
}
