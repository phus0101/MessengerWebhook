namespace MessengerWebhook.Services.Admin;

public interface IAdminDraftOrderService
{
    Task<AdminCommandResult> UpdateDraftOrderAsync(
        AdminUserContext user,
        Guid draftOrderId,
        UpdateDraftOrderRequest request,
        CancellationToken cancellationToken = default);
}
