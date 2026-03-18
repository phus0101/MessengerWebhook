namespace MessengerWebhook.Configuration;

/// <summary>
/// Webhook configuration options
/// </summary>
public class WebhookOptions
{
    public const string SectionName = "Webhook";

    /// <summary>
    /// Verify token for webhook subscription
    /// </summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>
    /// Timeout for webhook processing in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Channel capacity for background processing queue
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;
}
