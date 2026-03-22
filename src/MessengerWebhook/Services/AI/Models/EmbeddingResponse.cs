namespace MessengerWebhook.Services.AI.Models;

public class EmbeddingResponse
{
    public EmbeddingData Embedding { get; set; } = null!;
}

public class EmbeddingData
{
    public float[] Values { get; set; } = Array.Empty<float>();
}

public class BatchEmbeddingResponse
{
    public List<EmbeddingResponse> Embeddings { get; set; } = new();
}
