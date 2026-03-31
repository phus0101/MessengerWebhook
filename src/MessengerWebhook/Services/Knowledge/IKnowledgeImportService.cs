using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Knowledge;

public interface IKnowledgeImportService
{
    Task<KnowledgeSnapshot> ImportTextAsync(
        KnowledgeCategory category,
        string sourceName,
        string sourceType,
        string content,
        bool publish = false,
        CancellationToken cancellationToken = default);
}
