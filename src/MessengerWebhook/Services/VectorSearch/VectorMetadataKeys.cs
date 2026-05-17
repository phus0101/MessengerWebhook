namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Canonical metadata key constants for Pinecone vector records.
/// Use these instead of magic strings to ensure consistency across upsert and query paths.
/// </summary>
public static class VectorMetadataKeys
{
    // Core product identifiers
    public const string ProductId = "product_id";
    public const string ProductCode = "product_code";
    public const string Name = "name";
    public const string Price = "price";
    public const string IsActive = "is_active";
    public const string Sku = "sku";
    public const string Category = "category";
    public const string Brand = "brand";

    // Tenant isolation
    public const string TenantId = "tenant_id";

    // Localisation
    public const string Locale = "locale";

    // Pricing / inventory
    public const string PriceEffectiveDate = "price_eff_date";
    public const string InventoryRegion = "inventory_region";

    // Content classification
    public const string ContentType = "content_type";
    public const string PolicyVersion = "policy_version";
    public const string ChannelVisibility = "channel_visibility";

    // Provenance
    public const string SourceUrl = "source_url";
}
