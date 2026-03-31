using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Customers;

public interface ICustomerIntelligenceService
{
    Task<CustomerIdentity?> GetExistingAsync(
        string facebookPsid,
        string? pageId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerIdentity> GetOrCreateAsync(
        string facebookPsid,
        string? pageId = null,
        string? phoneNumber = null,
        string? fullName = null,
        string? shippingAddress = null,
        CancellationToken cancellationToken = default);

    Task<VipProfile> GetVipProfileAsync(CustomerIdentity customer, CancellationToken cancellationToken = default);
    Task<RiskSignal> BuildRiskSignalAsync(CustomerIdentity customer, Guid? draftOrderId = null, CancellationToken cancellationToken = default);
}
