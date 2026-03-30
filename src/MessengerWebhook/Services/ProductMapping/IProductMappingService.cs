using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.ProductMapping;

/// <summary>
/// Service for mapping Quick Reply/Postback payloads to products
/// </summary>
public interface IProductMappingService
{
    /// <summary>
    /// Get product by Quick Reply or Postback payload
    /// Payload format: "PRODUCT_{CODE}" -> Product.Code
    /// </summary>
    Task<Product?> GetProductByPayloadAsync(string payload);

    /// <summary>
    /// Get product by code directly
    /// </summary>
    Task<Product?> GetProductByCodeAsync(string code);

    /// <summary>
    /// Check if payload format is valid
    /// </summary>
    bool IsValidPayload(string payload);
}
