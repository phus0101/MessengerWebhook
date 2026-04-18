namespace MessengerWebhook.Services.Metrics.Configuration;

public class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Maximum number of metrics to buffer before forcing a flush.
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval in seconds (even if batch size not reached).
    /// Default: 60 seconds
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Enable metrics collection.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
