using System.Text.RegularExpressions;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;

namespace MessengerWebhook.Services.ProductMapping;

/// <summary>
/// Maps quick reply payloads and customer language into product records.
/// </summary>
public partial class ProductMappingService(
    IProductRepository productRepository,
    IHybridSearchService hybridSearchService,
    ITenantContext tenantContext)
    : IProductMappingService
{
    private const int SemanticSearchTopK = 5;
    private static readonly Regex ProductCodeRegex = MyRegex();

    public async Task<Product?> GetProductByPayloadAsync(string payload)
    {
        if (!IsValidPayload(payload))
        {
            return null;
        }

        var code = payload.Replace("PRODUCT_", string.Empty, StringComparison.OrdinalIgnoreCase);
        return IsValidProductCode(code) ? await GetActiveProductByCodeAsync(code) : null;
    }

    public Task<Product?> GetProductByCodeAsync(string code)
    {
        return productRepository.GetByCodeAsync(code);
    }

    public Task<Product?> GetActiveProductByCodeAsync(string code)
    {
        return tenantContext.TenantId.HasValue
            ? productRepository.GetActiveByCodeAsync(code, tenantContext.TenantId.Value)
            : Task.FromResult<Product?>(null);
    }

    public async Task<Product?> GetProductByMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !tenantContext.TenantId.HasValue)
        {
            return null;
        }

        var tenantId = tenantContext.TenantId.Value;
        var directCode = ExtractProductCode(message);
        if (directCode != null)
        {
            var product = await productRepository.GetActiveByCodeAsync(directCode, tenantId);
            if (product != null)
            {
                return product;
            }
        }

        var filter = new Dictionary<string, object>
        {
            ["tenant_id"] = tenantId.ToString()
        };

        List<FusedResult> searchResults;
        try
        {
            searchResults = await hybridSearchService.SearchAsync(
                message,
                topK: SemanticSearchTopK,
                filter: filter);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        foreach (var result in searchResults)
        {
            if (string.IsNullOrWhiteSpace(result.ProductId))
            {
                continue;
            }

            var product = await productRepository.GetActiveByIdAsync(result.ProductId, tenantId);
            if (product != null)
            {
                return product;
            }
        }

        return null;
    }

    public bool IsValidPayload(string payload)
    {
        return !string.IsNullOrWhiteSpace(payload) &&
               payload.StartsWith("PRODUCT_", StringComparison.OrdinalIgnoreCase) &&
               payload.Length > "PRODUCT_".Length;
    }

    private static string? ExtractProductCode(string message)
    {
        var normalized = message.Trim().ToUpperInvariant();
        if (IsValidProductCode(normalized))
        {
            return normalized;
        }

        var parenthesizedCode = ParenthesizedProductCodeRegex().Matches(message)
            .Select(match => match.Groups["code"].Value.ToUpperInvariant())
            .FirstOrDefault(IsValidProductCode);

        return parenthesizedCode;
    }

    private static bool IsValidProductCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && ProductCodeRegex.IsMatch(code);
    }

    [GeneratedRegex(@"^[A-Z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"\((?<code>[A-Za-z0-9_]+)\)", RegexOptions.Compiled)]
    private static partial Regex ParenthesizedProductCodeRegex();
}
