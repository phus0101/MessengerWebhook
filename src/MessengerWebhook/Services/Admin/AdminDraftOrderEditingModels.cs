namespace MessengerWebhook.Services.Admin;

public sealed record UpdateDraftOrderItemRequest(
    string ProductCode,
    int Quantity,
    string? GiftCode);

public sealed record UpdateDraftOrderRequest(
    Guid? CustomerIdentityId,
    string? CustomerName,
    string CustomerPhone,
    string ShippingAddress,
    IReadOnlyList<UpdateDraftOrderItemRequest> Items);
