using MessengerWebhook.Services.Metrics.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Metrics;

public class MetricsBackgroundService : BackgroundService
{
    private readonly IConversationMetricsService _metricsService;
    private readonly MetricsOptions _options;
    private readonly ILogger<MetricsBackgroundService> _logger;

    public MetricsBackgroundService(
        IConversationMetricsService metricsService,
        IOptions<MetricsOptions> options,
        ILogger<MetricsBackgroundService> logger)
    {
        _metricsService = metricsService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Metrics background service started (Batch size: {BatchSize}, Flush interval: {FlushInterval}s)",
            _options.BatchSize,
            _options.FlushIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for flush interval
                await Task.Delay(TimeSpan.FromSeconds(_options.FlushIntervalSeconds), stoppingToken);

                var bufferSize = _metricsService.GetBufferSize();

                // Flush if buffer has items (either reached batch size or time elapsed)
                if (bufferSize > 0)
                {
                    _logger.LogDebug("Flushing metrics buffer (size: {BufferSize})", bufferSize);
                    await _metricsService.FlushAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                _logger.LogInformation("Metrics background service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics background service");
                // Continue running despite errors
            }
        }

        // Final flush on shutdown
        try
        {
            var remainingMetrics = _metricsService.GetBufferSize();
            if (remainingMetrics > 0)
            {
                _logger.LogInformation("Flushing {Count} remaining metrics on shutdown", remainingMetrics);
                await _metricsService.FlushAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics on shutdown");
        }

        _logger.LogInformation("Metrics background service stopped");
    }
}
