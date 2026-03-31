namespace MessengerWebhook.Configuration;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string ProModel { get; set; } = "gemini-1.5-pro";
    public string FlashLiteModel { get; set; } = "gemini-1.5-flash";
    public string EmbeddingModel { get; set; } = "gemini-embedding-2-preview";
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 60;
    public string SystemPromptPath { get; set; } = "Prompts/sales-closer-system-prompt.txt";
    public string ConfirmationDetectionPromptPath { get; set; } = "Prompts/confirmation-detection-prompt.txt";
    public bool EnableAiConfirmationDetection { get; set; } = true;
    public double ConfirmationConfidenceThreshold { get; set; } = 0.7;
    public int ConfirmationCacheTtlMinutes { get; set; } = 5;
    public bool EnableAiIntentDetection { get; set; } = true;
    public double IntentConfidenceThreshold { get; set; } = 0.7;
    public RateLimitOptions RateLimits { get; set; } = new();
}

public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 60;
    public int TokensPerMinute { get; set; } = 100000;
}
