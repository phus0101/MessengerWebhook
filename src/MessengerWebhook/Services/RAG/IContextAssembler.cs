namespace MessengerWebhook.Services.RAG;

public interface IContextAssembler
{
    Task<AssembledRAGContext> AssembleContextAsync(
        List<string> productIds,
        bool includeDetailedInfo = false,
        CancellationToken cancellationToken = default);
}
