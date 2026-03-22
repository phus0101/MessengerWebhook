using MessengerWebhook.Data.Repositories;

namespace MessengerWebhook.BackgroundServices;

public class MessageCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageCleanupService> _logger;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public MessageCleanupService(
        IServiceProvider serviceProvider,
        ILogger<MessageCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next 2 AM UTC
                var now = DateTime.UtcNow;
                var next2Am = now.Date.AddDays(1).AddHours(2);
                if (now.Hour < 2)
                {
                    next2Am = now.Date.AddHours(2);
                }

                var delay = next2Am - now;
                _logger.LogDebug("Next message cleanup scheduled at {NextRun} (in {Delay})", next2Am, delay);

                await Task.Delay(delay, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IConversationMessageRepository>();

                var cutoffDate = DateTime.UtcNow - RetentionPeriod;
                _logger.LogInformation("Running message cleanup for messages older than {CutoffDate}", cutoffDate);

                await messageRepository.DeleteOlderThanAsync(cutoffDate, stoppingToken);
                _logger.LogInformation("Message cleanup completed");
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message cleanup");
            }
        }

        _logger.LogInformation("MessageCleanupService stopped");
    }
}
