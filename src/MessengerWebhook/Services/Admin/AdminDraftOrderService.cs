using System.Text;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.Nobita;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Admin;

public class AdminDraftOrderService : IAdminDraftOrderService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IFreeshipCalculator _freeshipCalculator;
    private readonly IAdminAuditService _adminAuditService;
    private readonly INobitaClient _nobitaClient;
    private readonly SalesBotOptions _salesBotOptions;

    public AdminDraftOrderService(
        MessengerBotDbContext dbContext,
        IFreeshipCalculator freeshipCalculator,
        IAdminAuditService adminAuditService,
        INobitaClient nobitaClient,
        IOptions<SalesBotOptions> salesBotOptions)
    {
        _dbContext = dbContext;
        _freeshipCalculator = freeshipCalculator;
        _adminAuditService = adminAuditService;
        _nobitaClient = nobitaClient;
        _salesBotOptions = salesBotOptions.Value;
    }

    public async Task<AdminCommandResult> UpdateDraftOrderAsync(
        AdminUserContext user,
        Guid draftOrderId,
        UpdateDraftOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var draft = await LoadDraftAsync(user, draftOrderId, cancellationToken);
        if (draft == null)
        {
            return new AdminCommandResult(false, "Khong tim thay don nhap.");
        }

        if (draft.Status == DraftOrderStatus.SubmittedToNobita)
        {
            return new AdminCommandResult(false, "Don da gui sang Nobita, khong the chinh sua.");
        }

        if (draft.SubmissionClaimedAt != null)
        {
            return new AdminCommandResult(false, "Don dang duoc gui sang Nobita, vui long thu lai sau.");
        }

        var validation = await ValidateRequestAsync(user, request, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation.Result!;
        }

        var normalizedName = NormalizeOptional(request.CustomerName);
        var normalizedPhone = NormalizeRequired(request.CustomerPhone);
        var normalizedAddress = NormalizeRequired(request.ShippingAddress);
        var normalizedItems = request.Items
            .Select(x => new UpdateDraftOrderItemRequest(
                NormalizeRequired(x.ProductCode).ToUpperInvariant(),
                x.Quantity,
                NormalizeOptional(x.GiftCode)?.ToUpperInvariant()))
            .ToList();

        var selectedCustomer = await ResolveSelectedCustomerAsync(user, request.CustomerIdentityId, cancellationToken);
        if (request.CustomerIdentityId.HasValue && selectedCustomer == null)
        {
            return new AdminCommandResult(false, "Khong tim thay khach hang hop le de nap thong tin vao form.");
        }

        var productCodes = normalizedItems.Select(x => x.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var products = await _dbContext.Products
            .Where(x => (x.TenantId == user.TenantId || x.TenantId == null) && productCodes.Contains(x.Code) && x.IsActive)
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var giftCodes = normalizedItems
            .Where(x => !string.IsNullOrWhiteSpace(x.GiftCode))
            .Select(x => x.GiftCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gifts = giftCodes.Count == 0
            ? new Dictionary<string, Gift>(StringComparer.OrdinalIgnoreCase)
            : await _dbContext.Gifts
                .Where(x => (x.TenantId == user.TenantId || x.TenantId == null) && giftCodes.Contains(x.Code) && x.IsActive)
                .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var allowedGiftPairs = await _dbContext.ProductGiftMappings
            .Where(x => (x.TenantId == user.TenantId || x.TenantId == null) && productCodes.Contains(x.ProductCode))
            .Select(x => new { x.ProductCode, x.GiftCode })
            .ToListAsync(cancellationToken);

        var allowedGiftLookup = allowedGiftPairs
            .GroupBy(x => x.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.GiftCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var existingItems = await _dbContext.DraftOrderItems
            .Where(x => x.DraftOrderId == draft.Id)
            .ToListAsync(cancellationToken);
        if (existingItems.Count > 0)
        {
            _dbContext.DraftOrderItems.RemoveRange(existingItems);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var rebuiltItems = new List<DraftOrderItem>();

        foreach (var item in normalizedItems)
        {
            var product = products[item.ProductCode];
            Gift? gift = null;

            if (!string.IsNullOrWhiteSpace(item.GiftCode))
            {
                if (!allowedGiftLookup.TryGetValue(product.Code, out var allowedGiftCodes) || !allowedGiftCodes.Contains(item.GiftCode))
                {
                    return new AdminCommandResult(false, $"Qua tang {item.GiftCode} khong hop le cho san pham {product.Code}.");
                }

                if (!gifts.TryGetValue(item.GiftCode, out gift))
                {
                    return new AdminCommandResult(false, $"Khong tim thay qua tang noi bo: {item.GiftCode}.");
                }
            }

            rebuiltItems.Add(new DraftOrderItem
            {
                DraftOrderId = draft.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.BasePrice,
                GiftCode = gift?.Code,
                GiftName = gift?.Name
            });
        }

        var expandedProductCodes = rebuiltItems
            .SelectMany(x => Enumerable.Repeat(x.ProductCode, Math.Max(x.Quantity, 1)))
            .ToList();

        _dbContext.DraftOrderItems.AddRange(rebuiltItems);

        draft.CustomerName = normalizedName;
        draft.CustomerPhone = normalizedPhone;
        draft.ShippingAddress = normalizedAddress;
        draft.MerchandiseTotal = rebuiltItems.Sum(x => x.UnitPrice * x.Quantity);
        draft.ShippingFee = _freeshipCalculator.CalculateShippingFee(expandedProductCodes);
        draft.GrandTotal = draft.MerchandiseTotal + draft.ShippingFee;
        draft.PriceConfirmed = true;
        draft.PromotionConfirmed = false;
        draft.ShippingConfirmed = true;
        draft.InventoryConfirmed = false;
        draft.Status = DraftOrderStatus.PendingReview;
        draft.ReviewedAt = null;
        draft.ReviewedByEmail = null;
        draft.LastSubmissionError = null;
        draft.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var riskSignal = await RebuildRiskSignalAsync(user, draft, normalizedPhone, selectedCustomer, cancellationToken);
        draft.RiskLevel = riskSignal.Level;
        draft.RiskSummary = riskSignal.Reason;
        draft.RequiresManualReview = riskSignal.RequiresManualReview;
        draft.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _adminAuditService.LogAsync(
            user,
            "update-draft",
            "draft-order",
            draft.Id.ToString(),
            BuildAuditDetails(draft, normalizedItems, request.CustomerIdentityId),
            cancellationToken);

        return new AdminCommandResult(true, "Da luu thay doi don nhap.");
    }

    private async Task<(bool Succeeded, AdminCommandResult? Result)> ValidateRequestAsync(
        AdminUserContext user,
        UpdateDraftOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerPhone) || string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return (false, new AdminCommandResult(false, "Don nhap phai co so dien thoai va dia chi."));
        }

        if (request.Items.Count == 0)
        {
            return (false, new AdminCommandResult(false, "Don nhap phai co it nhat 1 san pham."));
        }

        if (request.Items.Any(x => string.IsNullOrWhiteSpace(x.ProductCode) || x.Quantity <= 0))
        {
            return (false, new AdminCommandResult(false, "Moi dong san pham phai co ma hop le va so luong lon hon 0."));
        }

        var productCodes = request.Items
            .Select(x => NormalizeRequired(x.ProductCode).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var products = await _dbContext.Products
            .Where(x => (x.TenantId == user.TenantId || x.TenantId == null) && productCodes.Contains(x.Code) && x.IsActive)
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var missingProducts = productCodes
            .Except(products, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingProducts.Count > 0)
        {
            return (false, new AdminCommandResult(false, $"Khong tim thay san pham noi bo: {string.Join(", ", missingProducts)}"));
        }

        return (true, null);
    }

    private Task<CustomerIdentity?> ResolveSelectedCustomerAsync(
        AdminUserContext user,
        Guid? customerIdentityId,
        CancellationToken cancellationToken)
    {
        if (customerIdentityId == null)
        {
            return Task.FromResult<CustomerIdentity?>(null);
        }

        return _dbContext.CustomerIdentities.FirstOrDefaultAsync(
            x => x.Id == customerIdentityId.Value &&
                 x.TenantId == user.TenantId &&
                 (user.CanAccessAllPagesInTenant || user.FacebookPageId == null || x.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId),
            cancellationToken);
    }

    private async Task<RiskSignal> RebuildRiskSignalAsync(
        AdminUserContext user,
        DraftOrder draft,
        string phoneNumber,
        CustomerIdentity? selectedCustomer,
        CancellationToken cancellationToken)
    {
        var matchedCustomer = selectedCustomer ?? await _dbContext.CustomerIdentities
            .AsNoTracking()
            .Where(x =>
                x.TenantId == user.TenantId &&
                x.PhoneNumber == phoneNumber &&
                (user.CanAccessAllPagesInTenant || user.FacebookPageId == null || x.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId))
            .OrderByDescending(x => x.LastInteractionAt)
            .FirstOrDefaultAsync(cancellationToken);

        var insight = await _nobitaClient.TryGetCustomerInsightAsync(
            phoneNumber,
            matchedCustomer?.FacebookPSID ?? draft.FacebookPSID,
            cancellationToken);

        var totalOrders = Math.Max(matchedCustomer?.TotalOrders ?? 0, 1);
        var localRiskScore = (matchedCustomer?.FailedDeliveries ?? 0) / (decimal)totalOrders;
        var riskScore = insight?.RiskScore ?? localRiskScore;
        var riskLevel = riskScore >= _salesBotOptions.HighRiskThreshold
            ? RiskLevel.High
            : riskScore > 0
                ? RiskLevel.Medium
                : RiskLevel.Low;

        var riskSignal = new RiskSignal
        {
            TenantId = draft.TenantId,
            CustomerIdentityId = matchedCustomer?.Id,
            DraftOrderId = draft.Id,
            Score = riskScore,
            Level = riskLevel,
            Source = insight == null ? "local" : "nobita",
            Reason = riskLevel == RiskLevel.High
                ? "Customer has elevated delivery risk and should be manually reviewed"
                : "No critical risk detected",
            RequiresManualReview = riskLevel == RiskLevel.High
        };

        _dbContext.RiskSignals.Add(riskSignal);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return riskSignal;
    }

    private Task<DraftOrder?> LoadDraftAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken)
    {
        return _dbContext.DraftOrders
            .FirstOrDefaultAsync(
                x => x.Id == draftOrderId &&
                     x.TenantId == user.TenantId &&
                     (user.CanAccessAllPagesInTenant || user.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId),
                cancellationToken);
    }

    private static string NormalizeRequired(string value)
    {
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string BuildAuditDetails(DraftOrder draft, IReadOnlyList<UpdateDraftOrderItemRequest> items, Guid? selectedCustomerId)
    {
        var builder = new StringBuilder();
        builder.Append("PrefilledFromCustomerId=").Append(selectedCustomerId?.ToString() ?? "none");
        builder.Append("; ");
        builder.Append("Name=").Append(draft.CustomerName ?? "none");
        builder.Append("; ");
        builder.Append("Phone=").Append(draft.CustomerPhone);
        builder.Append("; Address=").Append(draft.ShippingAddress);
        builder.Append("; Items=").Append(string.Join(", ", items.Select(x => $"{x.ProductCode}x{x.Quantity}:{x.GiftCode ?? "none"}")));
        builder.Append("; Total=").Append(draft.GrandTotal);
        return builder.ToString();
    }
}
