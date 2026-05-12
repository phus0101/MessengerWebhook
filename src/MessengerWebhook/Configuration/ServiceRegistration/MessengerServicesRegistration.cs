using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.LiveComments;
using MessengerWebhook.Services.Nobita;
using MessengerWebhook.Services.QuickReply;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class MessengerServicesRegistration
{
    internal static IServiceCollection AddMessengerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FacebookOptions>(configuration.GetSection(FacebookOptions.SectionName));
        services.AddSingleton<IValidateOptions<FacebookOptions>, ValidateFacebookOptions>();
        services.AddOptions<FacebookOptions>().ValidateOnStart();

        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.SectionName));
        services.AddSingleton<IValidateOptions<WebhookOptions>, ValidateWebhookOptions>();
        services.AddOptions<WebhookOptions>().ValidateOnStart();

        services.Configure<LiveCommentOptions>(configuration.GetSection(LiveCommentOptions.SectionName));
        services.Configure<NobitaOptions>(configuration.GetSection(NobitaOptions.SectionName));

        services.AddSingleton<ISignatureValidator, SignatureValidator>();
        services.AddScoped<WebhookProcessor>();
        services.AddScoped<ILiveCommentAutomationService, LiveCommentAutomationService>();
        services.AddScoped<IQuickReplyHandler, QuickReplyHandler>();

        services.AddHttpClient<IMessengerService, MessengerService>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
            });

        services.AddHttpClient<INobitaClient, NobitaClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<NobitaOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            {
                client.DefaultRequestHeaders.Remove("X-Api-Key");
                client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
            }
        });

        return services;
    }
}
