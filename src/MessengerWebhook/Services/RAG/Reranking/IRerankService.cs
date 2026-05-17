namespace MessengerWebhook.Services.RAG.Reranking;

public interface IRerankService
{
    Task<IReadOnlyList<RankedDocument>> RerankAsync(
        string query,
        IReadOnlyList<RankableDocument> candidates,
        int topN,
        CancellationToken ct = default);
}
