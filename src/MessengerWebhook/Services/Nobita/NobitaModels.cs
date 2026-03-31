namespace MessengerWebhook.Services.Nobita;

public sealed record NobitaProductSummary(int ProductId, string Code, string Name, decimal Price, bool IsOutOfStock);

public sealed record NobitaCustomerInsight(bool IsExisting, bool IsVip, decimal RiskScore, int TotalOrders);

public sealed record NobitaOrderLine(int ProductId, int Quantity, decimal Price, decimal Weight = 0);

public sealed record NobitaOrderRequest(
    string CustomerName,
    string CustomerPhoneNumber,
    string ShippingAddress,
    IReadOnlyList<NobitaOrderLine> Details,
    decimal Total,
    string? CustomerNotes = null);
