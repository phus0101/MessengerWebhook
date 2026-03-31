using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Nobita;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.Admin;

public class AdminDashboardQueryService : IAdminDashboardQueryService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly INobitaClient _nobitaClient;

    public AdminDashboardQueryService(MessengerBotDbContext dbContext, INobitaClient nobitaClient)
    {
        _dbContext = dbContext;
        _nobitaClient = nobitaClient;
    }

    public async Task<AdminOverviewDto> GetOverviewAsync(AdminUserContext user, CancellationToken cancellationToken = default)
    {
        return new AdminOverviewDto(
            await FilterDraftOrders(user).CountAsync(x => x.Status == DraftOrderStatus.PendingReview, cancellationToken),
            await FilterDraftOrders(user).CountAsync(x => x.Status == DraftOrderStatus.SubmitFailed, cancellationToken),
            await FilterSupportCases(user).CountAsync(x => x.Status == SupportCaseStatus.Open, cancellationToken),
            await FilterSupportCases(user).CountAsync(x => x.Status == SupportCaseStatus.Claimed, cancellationToken));
    }

    public async Task<IReadOnlyList<AdminDraftOrderListItemDto>> GetDraftOrdersAsync(AdminUserContext user, CancellationToken cancellationToken = default)
    {
        return await FilterDraftOrders(user)
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new AdminDraftOrderListItemDto(
                x.Id,
                x.DraftCode,
                x.FacebookPageId,
                x.CustomerName,
                x.CustomerPhone,
                x.ShippingAddress,
                x.Status,
                x.RiskLevel,
                x.RequiresManualReview,
                x.AssignedManagerEmail,
                x.Items.Count,
                x.GrandTotal,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminDraftOrderDetailDto?> GetDraftOrderAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default)
    {
        var draft = await FilterDraftOrders(user)
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == draftOrderId, cancellationToken);

        if (draft == null)
        {
            return null;
        }

        var auditLogs = await GetAuditLogsAsync(user, "draft-order", draft.Id.ToString(), cancellationToken);
        return new AdminDraftOrderDetailDto(
            draft.Id,
            draft.DraftCode,
            draft.FacebookPageId,
            draft.CustomerName,
            draft.CustomerPhone,
            draft.ShippingAddress,
            draft.Status,
            draft.RiskLevel,
            draft.RiskSummary,
            draft.RequiresManualReview,
            draft.MerchandiseTotal,
            draft.ShippingFee,
            draft.GrandTotal,
            draft.AssignedManagerEmail,
            draft.NobitaOrderId,
            draft.LastSubmissionError,
            draft.CreatedAt,
            draft.ReviewedAt,
            draft.ReviewedByEmail,
            draft.SubmittedAt,
            draft.SubmittedByEmail,
            draft.Items.Select(x => new AdminDraftOrderItemDto(x.Id, x.ProductCode, x.ProductName, x.Quantity, x.UnitPrice, x.GiftCode, x.GiftName)).ToList(),
            auditLogs);
    }

    public async Task<IReadOnlyList<AdminSupportCaseListItemDto>> GetSupportCasesAsync(AdminUserContext user, CancellationToken cancellationToken = default)
    {
        return await FilterSupportCases(user)
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new AdminSupportCaseListItemDto(
                x.Id,
                x.FacebookPSID,
                x.FacebookPageId,
                x.Reason,
                x.Status,
                x.Summary,
                x.AssignedToEmail,
                x.CreatedAt,
                x.ClaimedAt,
                x.ResolvedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminSupportCaseDetailDto?> GetSupportCaseAsync(AdminUserContext user, Guid supportCaseId, CancellationToken cancellationToken = default)
    {
        var supportCase = await FilterSupportCases(user)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == supportCaseId, cancellationToken);

        if (supportCase == null)
        {
            return null;
        }

        var auditLogs = await GetAuditLogsAsync(user, "support-case", supportCase.Id.ToString(), cancellationToken);
        return new AdminSupportCaseDetailDto(
            supportCase.Id,
            supportCase.FacebookPSID,
            supportCase.FacebookPageId,
            supportCase.Reason,
            supportCase.Status,
            supportCase.Summary,
            supportCase.TranscriptExcerpt,
            supportCase.AssignedToEmail,
            supportCase.ClaimedByEmail,
            supportCase.ResolvedByEmail,
            supportCase.ResolutionNotes,
            supportCase.CreatedAt,
            supportCase.ClaimedAt,
            supportCase.ResolvedAt,
            auditLogs);
    }

    public async Task<IReadOnlyList<AdminProductMappingDto>> GetProductMappingsAsync(AdminUserContext user, string? search, CancellationToken cancellationToken = default)
    {
        var query = FilterProducts(user).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new AdminProductMappingDto(
                x.Id,
                x.Code,
                x.Name,
                x.BasePrice,
                x.NobitaProductId,
                x.NobitaWeight,
                x.NobitaLastSyncedAt,
                x.NobitaSyncError))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NobitaProductOptionDto>> SearchNobitaProductsAsync(string? search, CancellationToken cancellationToken = default)
    {
        var products = await _nobitaClient.GetProductsAsync(search, cancellationToken);
        return products
            .Select(x => new NobitaProductOptionDto(x.ProductId, x.Code, x.Name, x.Price, x.IsOutOfStock))
            .ToList();
    }

    private IQueryable<DraftOrder> FilterDraftOrders(AdminUserContext user)
    {
        return _dbContext.DraftOrders
            .Where(x => x.TenantId == user.TenantId &&
                        (user.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId));
    }

    private IQueryable<HumanSupportCase> FilterSupportCases(AdminUserContext user)
    {
        return _dbContext.HumanSupportCases
            .Where(x => x.TenantId == user.TenantId &&
                        (user.FacebookPageId == null || x.FacebookPageId == user.FacebookPageId));
    }

    private IQueryable<Product> FilterProducts(AdminUserContext user)
    {
        return _dbContext.Products.Where(x => x.TenantId == user.TenantId);
    }

    private Task<List<AdminAuditLogDto>> GetAuditLogsAsync(
        AdminUserContext user,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        return _dbContext.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.TenantId == user.TenantId &&
                        x.ResourceType == resourceType &&
                        x.ResourceId == resourceId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminAuditLogDto(
                x.Id,
                x.ActorEmail,
                x.Action,
                x.ResourceType,
                x.ResourceId,
                x.Details,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
