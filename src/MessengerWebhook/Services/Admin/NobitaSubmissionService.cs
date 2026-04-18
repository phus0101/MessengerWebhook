using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Nobita;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.Admin;

public class NobitaSubmissionService : INobitaSubmissionService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly INobitaClient _nobitaClient;
    private readonly IAdminAuditService _adminAuditService;

    public NobitaSubmissionService(
        MessengerBotDbContext dbContext,
        INobitaClient nobitaClient,
        IAdminAuditService adminAuditService)
    {
        _dbContext = dbContext;
        _nobitaClient = nobitaClient;
        _adminAuditService = adminAuditService;
    }

    public Task<AdminCommandResult> ApproveAndSubmitAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default)
    {
        return SubmitInternalAsync(user, draftOrderId, true, cancellationToken);
    }

    public async Task<AdminCommandResult> RejectAsync(AdminUserContext user, Guid draftOrderId, string? notes, CancellationToken cancellationToken = default)
    {
        var draft = await LoadDraftAsync(user, draftOrderId, cancellationToken);
        if (draft == null)
        {
            return new AdminCommandResult(false, "Không tìm thấy đơn nháp.");
        }

        draft.Status = DraftOrderStatus.Rejected;
        draft.ReviewedAt = DateTime.UtcNow;
        draft.ReviewedByEmail = user.Email;
        draft.LastSubmissionError = notes;
        draft.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminAuditService.LogAsync(user, "reject", "draft-order", draft.Id.ToString(), notes, cancellationToken);
        return new AdminCommandResult(true, "Đã từ chối đơn nháp.");
    }

    public Task<AdminCommandResult> RetrySubmitAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default)
    {
        return SubmitInternalAsync(user, draftOrderId, false, cancellationToken);
    }

    public async Task<AdminCommandResult> UpdateProductMappingAsync(AdminUserContext user, string productId, int nobitaProductId, decimal nobitaWeight, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == user.TenantId, cancellationToken);
        if (product == null)
        {
            return new AdminCommandResult(false, "Không tìm thấy sản phẩm.");
        }

        product.NobitaProductId = nobitaProductId;
        product.NobitaWeight = nobitaWeight;
        product.NobitaLastSyncedAt = DateTime.UtcNow;
        product.NobitaSyncError = null;
        product.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminAuditService.LogAsync(
            user,
            "map-product",
            "product",
            product.Id,
            $"Mapped {product.Code} -> {nobitaProductId}",
            cancellationToken);

        return new AdminCommandResult(true, "Đã cập nhật mapping sản phẩm.");
    }

    public async Task<IReadOnlyList<AdminProductMappingDto>> SyncProductsAsync(AdminUserContext user, string? search, CancellationToken cancellationToken = default)
    {
        var nobitaProducts = await _nobitaClient.GetProductsAsync(search, cancellationToken);
        var internalProducts = await _dbContext.Products
            .Where(x => x.TenantId == user.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var product in internalProducts)
        {
            var matched = nobitaProducts.FirstOrDefault(x => string.Equals(x.Code, product.Code, StringComparison.OrdinalIgnoreCase));
            if (matched == null)
            {
                continue;
            }

            product.NobitaProductId = matched.ProductId;
            product.NobitaLastSyncedAt = DateTime.UtcNow;
            product.NobitaSyncError = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminAuditService.LogAsync(
            user,
            "sync-nobita-products",
            "product",
            "*",
            $"Fetched {nobitaProducts.Count} products",
            cancellationToken);

        return internalProducts
            .OrderBy(x => x.Name)
            .Select(x => new AdminProductMappingDto(
                x.Id,
                x.Code,
                x.Name,
                x.BasePrice,
                x.NobitaProductId,
                x.NobitaWeight,
                x.NobitaLastSyncedAt,
                x.NobitaSyncError))
            .ToList();
    }

    private async Task<AdminCommandResult> SubmitInternalAsync(
        AdminUserContext user,
        Guid draftOrderId,
        bool forceApprove,
        CancellationToken cancellationToken)
    {
        var draft = await LoadDraftAsync(user, draftOrderId, cancellationToken);
        if (draft == null)
        {
            return new AdminCommandResult(false, "Không tìm thấy đơn nháp.");
        }

        if (draft.Status == DraftOrderStatus.SubmittedToNobita)
        {
            return new AdminCommandResult(false, "Đơn này đã được gửi sang Nobita.");
        }

        if (draft.SubmissionClaimedAt != null)
        {
            return new AdminCommandResult(false, "Đơn đang được gửi sang Nobita bởi một yêu cầu khác.");
        }

        var validationError = await ValidateDraftAsync(user, draft, cancellationToken);
        if (validationError != null)
        {
            return validationError;
        }

        var claimedAt = DateTime.UtcNow;
        draft.ReviewedAt ??= claimedAt;
        draft.ReviewedByEmail ??= user.Email;
        draft.Status = DraftOrderStatus.SubmittingToNobita;
        draft.SubmissionAttemptCount += 1;
        draft.LastSubmissionAttemptAt = claimedAt;
        draft.SubmissionClaimedAt = claimedAt;
        draft.SubmissionVersionToken = Guid.NewGuid();
        draft.LastSubmissionError = null;
        draft.UpdatedAt = claimedAt;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new AdminCommandResult(false, "Đơn đang được gửi sang Nobita bởi một yêu cầu khác.");
        }

        string? orderId;
        try
        {
            var products = await _dbContext.Products
                .Where(x => draft.Items.Select(i => i.ProductCode).Contains(x.Code) && (x.TenantId == user.TenantId || x.TenantId == null))
                .ToDictionaryAsync(x => x.Code, cancellationToken);

            orderId = await _nobitaClient.CreateOrderAsync(
                new NobitaOrderRequest(
                    draft.CustomerName ?? "Khách hàng Messenger",
                    draft.CustomerPhone,
                    draft.ShippingAddress,
                    draft.Items.Select(x => new NobitaOrderLine(
                        products[x.ProductCode].NobitaProductId!.Value,
                        x.Quantity,
                        x.UnitPrice,
                        products[x.ProductCode].NobitaWeight)).ToList(),
                    draft.GrandTotal,
                    draft.CustomerNotes),
                cancellationToken);
        }
        catch (Exception ex)
        {
            draft.Status = DraftOrderStatus.SubmitFailed;
            draft.SubmissionClaimedAt = null;
            draft.LastSubmissionError = ex.Message;
            draft.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _adminAuditService.LogAsync(user, "submit-failed", "draft-order", draft.Id.ToString(), ex.Message, cancellationToken);
            return new AdminCommandResult(false, "Gửi Nobita thất bại.", ex.Message);
        }

        draft.Status = DraftOrderStatus.SubmittedToNobita;
        draft.SubmittedAt = DateTime.UtcNow;
        draft.SubmittedByEmail = user.Email;
        draft.NobitaOrderId = orderId;
        draft.SubmissionClaimedAt = null;
        draft.LastSubmissionError = null;
        draft.UpdatedAt = DateTime.UtcNow;
        await TryApplyCustomerMetricsAsync(draft, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminAuditService.LogAsync(user, "approve-submit", "draft-order", draft.Id.ToString(), orderId, cancellationToken);
        return new AdminCommandResult(true, "Đã gửi đơn sang Nobita.", orderId);
    }

    private async Task<AdminCommandResult?> ValidateDraftAsync(AdminUserContext user, DraftOrder draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.CustomerPhone) || string.IsNullOrWhiteSpace(draft.ShippingAddress))
        {
            return new AdminCommandResult(false, "Đơn nháp chưa đủ số điện thoại và địa chỉ.");
        }

        if (draft.Items.Count == 0)
        {
            return new AdminCommandResult(false, "Đơn nháp chưa có sản phẩm.");
        }

        if (!draft.PriceConfirmed || !draft.ShippingConfirmed)
        {
            return new AdminCommandResult(false, "Đơn nháp chưa chốt đủ giá và phí ship để gửi xuống hệ thống.");
        }

        var products = await _dbContext.Products
            .Where(x => draft.Items.Select(i => i.ProductCode).Contains(x.Code) && (x.TenantId == user.TenantId || x.TenantId == null))
            .ToListAsync(cancellationToken);

        var missingMappings = products
            .Where(x => x.NobitaProductId == null)
            .Select(x => x.Code)
            .ToList();
        if (missingMappings.Count > 0)
        {
            return new AdminCommandResult(false, $"Các mã chưa map Nobita: {string.Join(", ", missingMappings)}");
        }

        var missingProducts = draft.Items
            .Where(x => products.All(p => p.Code != x.ProductCode))
            .Select(x => x.ProductCode)
            .ToList();
        if (missingProducts.Count > 0)
        {
            return new AdminCommandResult(false, $"Không tìm thấy sản phẩm nội bộ: {string.Join(", ", missingProducts)}");
        }

        return null;
    }

    private async Task TryApplyCustomerMetricsAsync(DraftOrder draft, CancellationToken cancellationToken)
    {
        if (draft.CustomerIdentityId is not Guid customerIdentityId || draft.CustomerMetricsAppliedAt != null)
        {
            return;
        }

        var customer = await _dbContext.CustomerIdentities.FirstOrDefaultAsync(x => x.Id == customerIdentityId, cancellationToken);
        if (customer == null)
        {
            draft.CustomerMetricsAppliedAt = DateTime.UtcNow;
            return;
        }

        customer.TotalOrders += 1;
        customer.LifetimeValue += draft.GrandTotal;
        customer.UpdatedAt = DateTime.UtcNow;
        draft.CustomerMetricsAppliedAt = DateTime.UtcNow;
    }

    private Task<DraftOrder?> LoadDraftAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken)
    {
        return _dbContext.DraftOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == draftOrderId &&
                     x.TenantId == user.TenantId &&
                     (user.CanAccessAllPagesInTenant || user.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId),
                cancellationToken);
    }
}
