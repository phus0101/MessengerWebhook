using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.StateMachine.Handlers;

internal static class SalesHandlerFallbacks
{
    public static IPolicyGuardService PolicyGuardService { get; } =
        new PolicyGuardService(Microsoft.Extensions.Options.Options.Create(new SalesBotOptions()));

    public static IProductMappingService ProductMappingService { get; } = new EmptyProductMappingService();
    public static IGiftSelectionService GiftSelectionService { get; } = new EmptyGiftSelectionService();
    public static IFreeshipCalculator FreeshipCalculator { get; } = new FreeshipCalculator();
    public static ICaseEscalationService CaseEscalationService { get; } = new EmptyCaseEscalationService();
    public static IDraftOrderService DraftOrderService { get; } = new EmptyDraftOrderService();
    public static ICustomerIntelligenceService CustomerIntelligenceService { get; } = new EmptyCustomerIntelligenceService();
    public static IOptions<SalesBotOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new SalesBotOptions());

    private sealed class EmptyProductMappingService : IProductMappingService
    {
        public Task<Product?> GetProductByPayloadAsync(string payload) => Task.FromResult<Product?>(null);
        public Task<Product?> GetProductByCodeAsync(string code) => Task.FromResult<Product?>(null);
        public Task<Product?> GetProductByMessageAsync(string message) => Task.FromResult<Product?>(null);
        public bool IsValidPayload(string payload) => false;
    }

    private sealed class EmptyGiftSelectionService : IGiftSelectionService
    {
        public Task<Gift?> SelectGiftForProductAsync(string productCode) => Task.FromResult<Gift?>(null);
        public Task<List<Gift>> GetAvailableGiftsForProductAsync(string productCode) => Task.FromResult(new List<Gift>());
    }

    private sealed class EmptyCaseEscalationService : ICaseEscalationService
    {
        public Task<HumanSupportCase> EscalateAsync(
            string facebookPsid,
            SupportCaseReason reason,
            string summary,
            string transcriptExcerpt,
            Guid? draftOrderId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HumanSupportCase
            {
                FacebookPSID = facebookPsid,
                Reason = reason,
                Summary = summary,
                TranscriptExcerpt = transcriptExcerpt
            });
        }
    }

    private sealed class EmptyDraftOrderService : IDraftOrderService
    {
        public Task<DraftOrder> CreateFromContextAsync(StateMachine.Models.StateContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Draft order service is not configured for this handler instance.");
        }
    }

    private sealed class EmptyCustomerIntelligenceService : ICustomerIntelligenceService
    {
        public Task<CustomerIdentity> GetOrCreateAsync(
            string facebookPsid,
            string? pageId = null,
            string? phoneNumber = null,
            string? fullName = null,
            string? shippingAddress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CustomerIdentity
            {
                FacebookPSID = facebookPsid,
                FacebookPageId = pageId,
                PhoneNumber = phoneNumber,
                FullName = fullName,
                ShippingAddress = shippingAddress
            });
        }

        public Task<VipProfile> GetVipProfileAsync(CustomerIdentity customer, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VipProfile
            {
                CustomerIdentityId = customer.Id,
                TotalOrders = customer.TotalOrders,
                IsVip = false
            });
        }

        public Task<RiskSignal> BuildRiskSignalAsync(CustomerIdentity customer, Guid? draftOrderId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RiskSignal
            {
                CustomerIdentityId = customer.Id,
                DraftOrderId = draftOrderId
            });
        }
    }
}
