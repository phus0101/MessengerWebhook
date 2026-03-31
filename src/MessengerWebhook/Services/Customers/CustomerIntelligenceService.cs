using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Nobita;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Customers;

public class CustomerIntelligenceService : ICustomerIntelligenceService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly INobitaClient _nobitaClient;
    private readonly SalesBotOptions _options;

    public CustomerIntelligenceService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        INobitaClient nobitaClient,
        IOptions<SalesBotOptions> options)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _nobitaClient = nobitaClient;
        _options = options.Value;
    }

    public async Task<CustomerIdentity> GetOrCreateAsync(
        string facebookPsid,
        string? pageId = null,
        string? phoneNumber = null,
        string? fullName = null,
        string? shippingAddress = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.CustomerIdentities
            .Include(x => x.VipProfile)
            .FirstOrDefaultAsync(x => x.FacebookPSID == facebookPsid, cancellationToken);

        if (customer == null)
        {
            customer = new CustomerIdentity
            {
                TenantId = _tenantContext.TenantId,
                FacebookPSID = facebookPsid,
                FacebookPageId = pageId ?? _tenantContext.FacebookPageId
            };
            _dbContext.CustomerIdentities.Add(customer);
        }

        customer.PhoneNumber = phoneNumber ?? customer.PhoneNumber;
        customer.FullName = fullName ?? customer.FullName;
        customer.ShippingAddress = shippingAddress ?? customer.ShippingAddress;
        customer.FacebookPageId = pageId ?? customer.FacebookPageId ?? _tenantContext.FacebookPageId;
        customer.LastInteractionAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<VipProfile> GetVipProfileAsync(CustomerIdentity customer, CancellationToken cancellationToken = default)
    {
        var insight = await TryGetInsightAsync(customer, cancellationToken);
        var vipProfile = await _dbContext.VipProfiles
            .FirstOrDefaultAsync(x => x.CustomerIdentityId == customer.Id, cancellationToken);

        if (vipProfile == null)
        {
            vipProfile = new VipProfile
            {
                TenantId = customer.TenantId,
                CustomerIdentityId = customer.Id
            };
            _dbContext.VipProfiles.Add(vipProfile);
        }

        vipProfile.TotalOrders = Math.Max(customer.TotalOrders, insight?.TotalOrders ?? 0);
        vipProfile.LifetimeValue = customer.LifetimeValue;
        vipProfile.IsVip = insight?.IsVip == true || vipProfile.TotalOrders >= _options.VipOrderThreshold;
        vipProfile.Tier = vipProfile.IsVip ? VipTier.Vip : vipProfile.TotalOrders > 0 ? VipTier.Returning : VipTier.Standard;
        vipProfile.GreetingStyle = vipProfile.IsVip
            ? "Da em chao chi khach quen cua Mui Xu a."
            : vipProfile.TotalOrders > 0
                ? "Da em chao chi, em ho tro chi tiep nha."
                : string.Empty;
        vipProfile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return vipProfile;
    }

    public async Task<RiskSignal> BuildRiskSignalAsync(
        CustomerIdentity customer,
        Guid? draftOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var insight = await TryGetInsightAsync(customer, cancellationToken);
        var totalOrders = Math.Max(customer.TotalOrders, 1);
        var localRiskScore = customer.FailedDeliveries / (decimal)totalOrders;
        var riskScore = insight?.RiskScore ?? localRiskScore;
        var riskLevel = riskScore >= _options.HighRiskThreshold
            ? RiskLevel.High
            : riskScore > 0
                ? RiskLevel.Medium
                : RiskLevel.Low;

        var signal = new RiskSignal
        {
            TenantId = customer.TenantId,
            CustomerIdentityId = customer.Id,
            DraftOrderId = draftOrderId,
            Score = riskScore,
            Level = riskLevel,
            Source = insight == null ? "local" : "nobita",
            Reason = riskLevel == RiskLevel.High
                ? "Customer has elevated delivery risk and should be manually reviewed"
                : "No critical risk detected",
            RequiresManualReview = riskLevel == RiskLevel.High
        };

        _dbContext.RiskSignals.Add(signal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return signal;
    }

    private async Task<NobitaCustomerInsight?> TryGetInsightAsync(CustomerIdentity customer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            return null;
        }

        return await _nobitaClient.TryGetCustomerInsightAsync(
            customer.PhoneNumber,
            customer.FacebookPSID,
            cancellationToken);
    }
}
