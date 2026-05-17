namespace MessengerWebhook.Configuration;

public class CohereOptions
{
    public const string SectionName = "Cohere";
    public string ApiKey { get; set; } = "";
    public string RerankModel { get; set; } = "rerank-multilingual-v3.0";
    public int TimeoutMs { get; set; } = 3000;
    public int CacheTtlMinutes { get; set; } = 10;
    public bool Enabled { get; set; } = true;
    public int CandidateMultiplier { get; set; } = 4;
}
