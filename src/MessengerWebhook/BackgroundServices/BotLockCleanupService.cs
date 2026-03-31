using MessengerWebhook.Data;
using MessengerWebhook.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.BackgroundServices;

public class BotLockCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BotLockCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public BotLockCleanupService(
        IServiceProvider serviceProvider,
        ILogger<BotLockCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bot Lock Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredLocksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired bot locks");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Bot Lock Cleanup Service stopped");
    }

    private async Task ProcessExpiredLocksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var botLockService = scope.ServiceProvider.GetRequiredService<IBotLockService>();

        var expiredLocks = await dbContext.BotConversationLocks
            .Where(x => x.IsLocked && x.UnlockAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var lockEntity in expiredLocks)
        {
            await botLockService.ReleaseAsync(lockEntity.FacebookPSID, cancellationToken);
            _logger.LogInformation(
                "Auto-unlocked bot for PSID {PSID} after timeout",
                lockEntity.FacebookPSID);
        }

        if (expiredLocks.Count > 0)
        {
            _logger.LogInformation("Processed {Count} expired bot locks", expiredLocks.Count);
        }
    }
}
