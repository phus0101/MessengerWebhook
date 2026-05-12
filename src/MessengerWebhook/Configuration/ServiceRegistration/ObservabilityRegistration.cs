using MessengerWebhook.HealthChecks;
using MessengerWebhook.Services.Notifications;
using Serilog;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class ObservabilityRegistration
{
    internal static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, config) => config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "MessengerWebhook")
            .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {TenantId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Conditional(
                _ => ctx.Configuration.GetValue<bool>("Seq:Enabled"),
                wt => wt.Seq(
                    serverUrl: ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341",
                    apiKey: ctx.Configuration["Seq:ApiKey"],
                    batchPostingLimit: 100,
                    period: TimeSpan.FromSeconds(2))));

        builder.Services.Configure<SeqOptions>(
            builder.Configuration.GetSection(SeqOptions.SectionName));

        builder.Services.Configure<TelegramOptions>(
            builder.Configuration.GetSection(TelegramOptions.SectionName));
        builder.Services.AddHttpClient<TelegramNotifier>();
        builder.Services.AddSingleton<AlertDeduplicator>();

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddHealthChecks()
            .AddCheck<ChannelHealthCheck>("channel_queue")
            .AddCheck<GraphApiHealthCheck>("graph_api");

        return builder;
    }
}
