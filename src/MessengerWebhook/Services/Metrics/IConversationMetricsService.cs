using MessengerWebhook.Services.Metrics.Models;

namespace MessengerWebhook.Services.Metrics;

public interface IConversationMetricsService
{
    /// <summary>
    /// Logs conversation metrics asynchronously (non-blocking, <10ms).
    /// Metrics are buffered and flushed in batches.
    /// </summary>
    Task LogAsync(ConversationMetricData metricData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current buffer size (for monitoring/debugging).
    /// </summary>
    int GetBufferSize();

    /// <summary>
    /// Forces immediate flush of buffered metrics (for testing/shutdown).
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
