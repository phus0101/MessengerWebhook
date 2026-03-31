namespace MessengerWebhook.Services.Admin;

public interface INobitaSubmissionService
{
    Task<AdminCommandResult> ApproveAndSubmitAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default);
    Task<AdminCommandResult> RejectAsync(AdminUserContext user, Guid draftOrderId, string? notes, CancellationToken cancellationToken = default);
    Task<AdminCommandResult> RetrySubmitAsync(AdminUserContext user, Guid draftOrderId, CancellationToken cancellationToken = default);
    Task<AdminCommandResult> UpdateProductMappingAsync(AdminUserContext user, string productId, int nobitaProductId, decimal nobitaWeight, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminProductMappingDto>> SyncProductsAsync(AdminUserContext user, string? search, CancellationToken cancellationToken = default);
}
