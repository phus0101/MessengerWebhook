using System.Threading.Channels;
using MessengerWebhook.Models;
using MessengerWebhook.Utilities;

namespace MessengerWebhook.BackgroundServices;

/// <summary>
/// Monitors channel capacity and logs warnings when approaching limits.
/// </summary>
public class ChannelMonitoringService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly Channel<MessagingEvent> _channel;
    private readonly ILogger<ChannelMonitoringService> _logger;
    private const int WarningThreshold = 800;  // 80% of 1000
    private const int CriticalThreshold = 950; // 95% of 1000

    public ChannelMonitoringService(
        Channel<MessagingEvent> channel,
        ILogger<ChannelMonitoringService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = _channel.Reader.Count;
                if (count >= CriticalThreshold)
                {
                    _logger.LogError("Channel at critical capacity: {Count}/1000 - events may be dropped!", count);
                }
                else if (count >= WarningThreshold)
                {
                    _logger.LogWarning("Channel approaching capacity: {Count}/1000", count);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).CatchIgnore();
        }
    }
}

public static class TaskExtensions
{
    public static async Task CatchIgnore(this Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
