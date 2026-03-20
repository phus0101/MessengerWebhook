namespace MessengerWebhook.Configuration;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string ProModel { get; set; } = "gemini-1.5-pro";
    public string FlashLiteModel { get; set; } = "gemini-1.5-flash";
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 60;
    public RateLimitOptions RateLimits { get; set; } = new();
}

public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 60;
    public int TokensPerMinute { get; set; } = 100000;
}
