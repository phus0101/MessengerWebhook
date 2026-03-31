using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;

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
}
