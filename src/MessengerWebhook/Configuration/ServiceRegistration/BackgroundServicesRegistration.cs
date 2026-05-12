using System.Threading.Channels;
using MessengerWebhook.BackgroundServices;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Survey;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class BackgroundServicesRegistration
{
    internal static IServiceCollection AddBotBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<WebhookProcessingService>();
        services.AddHostedService<SessionCleanupService>();
        services.AddHostedService<MessageCleanupService>();
        services.AddHostedService<BotLockCleanupService>();
        services.AddHostedService<ChannelMonitoringService>();
        services.AddHostedService<MessengerWebhook.Services.Metrics.MetricsBackgroundService>();
        services.AddHostedService<CSATSurveySchedulerService>();

        // Bounded channel for async webhook event processing
        var channel = Channel.CreateBounded<MessagingEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        services.AddSingleton(channel);

        // Limits concurrent live comment handlers to prevent ThreadPool exhaustion
        services.AddKeyedSingleton("liveComment", new SemaphoreSlim(50));

        return services;
    }
}
