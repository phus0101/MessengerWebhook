namespace MessengerWebhook.Services.AI;

public interface IEmbeddingService
{
    Task<float[]> GenerateAsync(string text, CancellationToken ct = default);
    Task<List<float[]>> GenerateBatchAsync(List<string> texts, CancellationToken ct = default);
}
