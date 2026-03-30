using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using System.Text.RegularExpressions;

namespace MessengerWebhook.Services.ProductMapping;

/// <summary>
/// Service for mapping Quick Reply/Postback payloads to products
/// </summary>
public class ProductMappingService : IProductMappingService
{
    private readonly IProductRepository _productRepository;
    private static readonly Regex ProductCodeRegex = new(@"^[A-Z0-9_]+$", RegexOptions.Compiled);

    public ProductMappingService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product?> GetProductByPayloadAsync(string payload)
    {
        if (!IsValidPayload(payload))
            return null;

        // Extract product code from payload
        // Format: "PRODUCT_{CODE}" -> CODE
        var code = payload.Replace("PRODUCT_", "", StringComparison.OrdinalIgnoreCase);

        // Validate extracted code format (alphanumeric and underscore only)
        if (!IsValidProductCode(code))
            return null;

        return await GetProductByCodeAsync(code);
    }

    public async Task<Product?> GetProductByCodeAsync(string code)
    {
        return await _productRepository.GetByCodeAsync(code);
    }

    public bool IsValidPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        // Valid format: "PRODUCT_{CODE}"
        return payload.StartsWith("PRODUCT_", StringComparison.OrdinalIgnoreCase)
               && payload.Length > 8;
    }

    private bool IsValidProductCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        // Only allow alphanumeric characters and underscores
        return ProductCodeRegex.IsMatch(code);
    }
}
