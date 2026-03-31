using System.Net.Http.Json;
using System.Text.Json;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Nobita;

public class NobitaClient : INobitaClient
{
    private readonly HttpClient _httpClient;
    private readonly NobitaOptions _options;
    private readonly ILogger<NobitaClient> _logger;

    public NobitaClient(
        HttpClient httpClient,
        IOptions<NobitaOptions> options,
        ILogger<NobitaClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NobitaProductSummary>> GetProductsAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var path = "products";
        if (!string.IsNullOrWhiteSpace(search))
        {
            path += $"?search={Uri.EscapeDataString(search)}";
        }

        var response = await _httpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Nobita product sync failed with status {StatusCode}", response.StatusCode);
            return Array.Empty<NobitaProductSummary>();
        }

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("data", out var data))
        {
            return Array.Empty<NobitaProductSummary>();
        }

        var products = new List<NobitaProductSummary>();
        foreach (var item in data.EnumerateArray())
        {
            products.Add(new NobitaProductSummary(
                item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                item.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                item.TryGetProperty("price", out var price) ? price.GetDecimal() : 0m,
                item.TryGetProperty("isOutOfStock", out var stock) && stock.GetBoolean()));
        }

        return products;
    }

    public async Task<NobitaCustomerInsight?> TryGetCustomerInsightAsync(
        string phoneNumber,
        string? facebookPsid = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableCustomerInsightLookup)
        {
            return null;
        }

        try
        {
            var payload = new { phone = phoneNumber, facebook_psid = facebookPsid };
            var response = await _httpClient.PostAsJsonAsync("customers/check", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Nobita customer insight endpoint unavailable: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            return new NobitaCustomerInsight(
                content.TryGetProperty("is_existing", out var isExisting) && isExisting.GetBoolean(),
                content.TryGetProperty("is_vip", out var isVip) && isVip.GetBoolean(),
                content.TryGetProperty("risk_score", out var risk) ? risk.GetDecimal() : 0m,
                content.TryGetProperty("total_orders", out var totalOrders) ? totalOrders.GetInt32() : 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to local customer intelligence for {Phone}", phoneNumber);
            return null;
        }
    }

    public async Task<string?> CreateOrderAsync(
        NobitaOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableDirectOrderSubmission)
        {
            return null;
        }

        var payload = new
        {
            invoice = new
            {
                discount = 0,
                type = 1,
                isDiscountPrice = true,
                vat = 0,
                total = request.Total,
                depositAmount = 0,
                transferAmount = 0,
                details = request.Details.Select(x => new
                {
                    productId = x.ProductId,
                    quantity = x.Quantity,
                    weight = x.Weight,
                    price = x.Price,
                    discount = 0,
                    isDiscountPrice = true
                })
            },
            customerName = request.CustomerName,
            customerNotes = request.CustomerNotes,
            customerPhoneNumber = request.CustomerPhoneNumber,
            shippingAddress = request.ShippingAddress,
            weight = request.Details.Sum(x => x.Weight),
            sourceName = "Messenger Sales Bot"
        };

        var response = await _httpClient.PostAsJsonAsync("orders", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Nobita order creation failed: {response.StatusCode} - {error}");
        }

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (content.ValueKind == JsonValueKind.Object && content.TryGetProperty("data", out var data))
        {
            if (data.TryGetProperty("id", out var id))
            {
                return id.ToString();
            }
        }

        return null;
    }
}
