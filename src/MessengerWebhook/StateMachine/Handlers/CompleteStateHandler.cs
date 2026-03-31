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

public class CompleteStateHandler : SalesStateHandlerBase
{
    public override ConversationState HandledState => ConversationState.Complete;

    public CompleteStateHandler(
        IGeminiService geminiService,
        IPolicyGuardService policyGuardService,
        IProductMappingService productMappingService,
        IGiftSelectionService giftSelectionService,
        IFreeshipCalculator freeshipCalculator,
        ICaseEscalationService caseEscalationService,
        IDraftOrderService draftOrderService,
        ICustomerIntelligenceService customerIntelligenceService,
        IOptions<SalesBotOptions> salesBotOptions,
        ILogger<CompleteStateHandler> logger)
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
        var draftCode = ctx.GetData<string>("draftOrderCode");
        var response = string.IsNullOrWhiteSpace(draftCode)
            ? "Dạ em da len don nhap cho chi roi a. Ben em se kiem tra lai thong tin va lien he xac nhan nha."
            : $"Dạ don nhap {draftCode} cua chi dang cho ben em kiem tra lai thong tin nha.";
        return Task.FromResult(response);
    }
}
