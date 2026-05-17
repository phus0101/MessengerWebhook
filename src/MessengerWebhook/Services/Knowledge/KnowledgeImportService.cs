using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;

namespace MessengerWebhook.Services.Knowledge;

public class KnowledgeImportService : IKnowledgeImportService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public KnowledgeImportService(MessengerBotDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<KnowledgeSnapshot> ImportTextAsync(
        KnowledgeCategory category,
        string sourceName,
        string sourceType,
        string content,
        bool publish = false,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new KnowledgeSnapshot
        {
            TenantId = _tenantContext.TenantId,
            Category = category,
            SourceName = sourceName,
            SourceType = sourceType,
            Content = content,
            Version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            IsPublished = publish,
            PublishedAt = publish ? DateTime.UtcNow : null
        };

        _dbContext.KnowledgeSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    /// <summary>
    /// Builds the Pinecone metadata dictionary for a knowledge snapshot,
    /// ready to pass to <c>IVectorSearchService.UpsertProductAsync</c>.
    /// Uses <see cref="VectorMetadataKeys"/> constants — no magic strings.
    /// Fields with well-known defaults are always populated for consistent filtering.
    /// </summary>
    public Dictionary<string, object> BuildSnapshotMetadata(KnowledgeSnapshot snapshot)
    {
        var metadata = new Dictionary<string, object>
        {
            [VectorMetadataKeys.TenantId]          = snapshot.TenantId?.ToString() ?? string.Empty,
            [VectorMetadataKeys.ContentType]        = ResolveContentType(snapshot.Category),
            [VectorMetadataKeys.Locale]             = "vi",       // default locale; override if multi-language support is added
            [VectorMetadataKeys.ChannelVisibility]  = "all",      // visible on all channels by default
            [VectorMetadataKeys.PolicyVersion]      = "v1",       // baseline policy version
        };

        // Product knowledge gets an inventory region tag so warehouse-scoped filters work
        if (snapshot.Category == KnowledgeCategory.Product)
        {
            metadata[VectorMetadataKeys.InventoryRegion] = "ALL";
        }

        return metadata;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>Maps <see cref="KnowledgeCategory"/> to a human-readable content type string.</summary>
    private static string ResolveContentType(KnowledgeCategory category) => category switch
    {
        KnowledgeCategory.Product   => "product",
        KnowledgeCategory.Gift      => "product",
        KnowledgeCategory.Faq       => "faq",
        KnowledgeCategory.Policy    => "policy",
        KnowledgeCategory.Privacy   => "policy",
        KnowledgeCategory.Promotion => "promotion",
        KnowledgeCategory.Shipping  => "policy",
        KnowledgeCategory.Inventory => "product",
        _                           => "general"
    };
}
