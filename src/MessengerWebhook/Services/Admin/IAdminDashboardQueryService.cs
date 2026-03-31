namespace MessengerWebhook.Services.Admin;

public interface IAdminDashboardQueryService
{
    Task<AdminOverviewDto> GetOverviewAsync(AdminUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminDraftOrderListItemDto>> GetDraftOrdersAsync(AdminUserContext user, CancellationToken cancellationToken = default);
    Task<AdminDraftOrderDetailDto?> GetDraftOrderAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminCustomerOptionDto>> SearchCustomersAsync(AdminUserContext user, string? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminGiftOptionDto>> GetGiftOptionsAsync(AdminUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminSupportCaseListItemDto>> GetSupportCasesAsync(AdminUserContext user, CancellationToken cancellationToken = default);
    Task<AdminSupportCaseDetailDto?> GetSupportCaseAsync(AdminUserContext user, Guid supportCaseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminProductMappingDto>> GetProductMappingsAsync(AdminUserContext user, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NobitaProductOptionDto>> SearchNobitaProductsAsync(string? search, CancellationToken cancellationToken = default);
}
