namespace MessengerWebhook.Services.Nobita;

public interface INobitaClient
{
    Task<IReadOnlyList<NobitaProductSummary>> GetProductsAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<NobitaCustomerInsight?> TryGetCustomerInsightAsync(string phoneNumber, string? facebookPsid = null, CancellationToken cancellationToken = default);
    Task<string?> CreateOrderAsync(NobitaOrderRequest request, CancellationToken cancellationToken = default);
}
