using MessengerWebhook.HealthChecks;
using MessengerWebhook.Services.Notifications;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {TenantId} {PsidHash} {Message:lj}{NewLine}{Exception}")
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

        // OTLP distributed tracing → Seq 2024.1+ ingest native (no collector needed)
        var otlpEndpoint = builder.Configuration["Seq:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            if (!Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
                throw new InvalidOperationException(
                    $"Seq:OtlpEndpoint '{otlpEndpoint}' is not a valid absolute URI.");

            var seqApiKey = builder.Configuration["Seq:ApiKey"];
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("MessengerWebhook"))
                .WithTracing(t => t
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = otlpUri;
                        o.Protocol = OtlpExportProtocol.HttpProtobuf;
                        if (!string.IsNullOrWhiteSpace(seqApiKey))
                            o.Headers = $"X-Seq-ApiKey={seqApiKey}";
                    }));
        }

        return builder;
    }
}
