using System.Diagnostics;
using System.Threading.Channels;
using MessengerWebhook.Models;
using MessengerWebhook.Services;

namespace MessengerWebhook.BackgroundServices;

/// <summary>
/// Background service that processes webhook events from Channel
/// </summary>
public class WebhookProcessingService : BackgroundService
{
    private readonly Channel<MessagingEvent> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookProcessingService> _logger;

    public WebhookProcessingService(
        Channel<MessagingEvent> channel,
        IServiceProvider serviceProvider,
        ILogger<WebhookProcessingService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook processing service started");

        await foreach (var messagingEvent in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();

                var stopwatch = Stopwatch.StartNew();
                await processor.ProcessAsync(messagingEvent);
                stopwatch.Stop();

                _logger.LogInformation(
                    "Event processed in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook event");
                // Don't throw - continue processing next events
            }
        }

        _logger.LogInformation("Webhook processing service stopped");
    }
}
