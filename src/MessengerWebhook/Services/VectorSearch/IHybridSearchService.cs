namespace MessengerWebhook.Services.VectorSearch;

/// <summary>
/// Interface for hybrid search combining vector and keyword search
/// </summary>
public interface IHybridSearchService
{
    /// <summary>
    /// Hybrid search combining vector and keyword search via RRF fusion
    /// </summary>
    Task<List<FusedResult>> SearchAsync(
        string query,
        int topK = 5,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);
}
