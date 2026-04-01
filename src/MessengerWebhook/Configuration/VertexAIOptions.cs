namespace MessengerWebhook.Configuration;

/// <summary>
/// Configuration options for Google Cloud Vertex AI
/// </summary>
public class VertexAIOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "asia-southeast1";
    public string Model { get; set; } = "text-embedding-004";
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
