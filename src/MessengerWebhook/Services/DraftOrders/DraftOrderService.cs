using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine.Models;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.DraftOrders;

public class DraftOrderService : IDraftOrderService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerIntelligenceService _customerIntelligenceService;
    private readonly ITenantContext _tenantContext;

    public DraftOrderService(
        MessengerBotDbContext dbContext,
        IProductRepository productRepository,
        ICustomerIntelligenceService customerIntelligenceService,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _productRepository = productRepository;
        _customerIntelligenceService = customerIntelligenceService;
        _tenantContext = tenantContext;
    }

    public async Task<DraftOrder> CreateFromContextAsync(StateContext context, CancellationToken cancellationToken = default)
    {
        var productCodes = context.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
        if (productCodes.Count == 0)
        {
            throw new InvalidOperationException("Cannot create draft order without selected products.");
        }

        var phoneNumber = context.GetData<string>("customerPhone") ?? string.Empty;
        var shippingAddress = context.GetData<string>("shippingAddress") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(shippingAddress))
        {
            throw new InvalidOperationException("Cannot create draft order without phone number and shipping address.");
        }

        var customer = await _customerIntelligenceService.GetOrCreateAsync(
            context.FacebookPSID,
            context.GetData<string>("facebookPageId") ?? _tenantContext.FacebookPageId,
            phoneNumber,
            context.GetData<string>("customerName"),
            shippingAddress,
            cancellationToken);

        var draftOrder = new DraftOrder
        {
            TenantId = _tenantContext.TenantId,
            DraftCode = $"DR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            SessionId = context.SessionId,
            CustomerIdentityId = customer.Id,
            FacebookPSID = context.FacebookPSID,
            FacebookPageId = customer.FacebookPageId ?? context.GetData<string>("facebookPageId") ?? _tenantContext.FacebookPageId,
            CustomerName = customer.FullName,
            CustomerPhone = phoneNumber,
            ShippingAddress = shippingAddress,
            ShippingFee = context.GetData<decimal?>("shippingFee") ?? 0,
            AssignedManagerEmail = _tenantContext.ManagerEmail
        };

        foreach (var productCode in productCodes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var product = await _productRepository.GetByCodeAsync(productCode);
            if (product == null)
            {
                continue;
            }

            draftOrder.Items.Add(new DraftOrderItem
            {
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = 1,
                UnitPrice = product.BasePrice,
                GiftCode = context.GetData<string>("selectedGiftCode"),
                GiftName = context.GetData<string>("selectedGiftName")
            });
        }

        if (draftOrder.Items.Count == 0)
        {
            throw new InvalidOperationException("Unable to resolve products for draft order.");
        }

        draftOrder.MerchandiseTotal = draftOrder.Items.Sum(x => x.UnitPrice * x.Quantity);
        draftOrder.GrandTotal = draftOrder.MerchandiseTotal + draftOrder.ShippingFee;

        _dbContext.DraftOrders.Add(draftOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var riskSignal = await _customerIntelligenceService.BuildRiskSignalAsync(customer, draftOrder.Id, cancellationToken);
        draftOrder.RiskLevel = riskSignal.Level;
        draftOrder.RiskSummary = riskSignal.Reason;
        draftOrder.RequiresManualReview = true;

        customer.TotalOrders += 1;
        customer.LifetimeValue += draftOrder.GrandTotal;
        customer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _customerIntelligenceService.GetVipProfileAsync(customer, cancellationToken);

        return await _dbContext.DraftOrders
            .Include(x => x.Items)
            .FirstAsync(x => x.Id == draftOrder.Id, cancellationToken);
    }
}
