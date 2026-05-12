using System.Threading.Channels;
using DotNetEnv;
using MessengerWebhook.Configuration.ServiceRegistration;
using MessengerWebhook.Endpoints;
using MessengerWebhook.Middleware;
using Serilog;

// Bootstrap Serilog before host configuration (enrichers loaded after builder)
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    Env.Load();
    var pineconeApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");
    if (!string.IsNullOrWhiteSpace(pineconeApiKey))
        builder.Configuration["Pinecone:ApiKey"] = pineconeApiKey;
}

builder.AddObservability();

builder.Services
    .AddPersistence(builder.Configuration)
    .AddAiServices(builder.Configuration)
    .AddCacheServices(builder.Configuration)
    .AddMessengerServices(builder.Configuration)
    .AddSalesPipeline(builder.Configuration)
    .AddAdminModule(builder.Configuration)
    .AddBotBackgroundServices();

var app = builder.Build();

await app.InitializeAsync();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SignatureValidationMiddleware>();
app.UseMiddleware<MetricsRateLimitMiddleware>();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<AdminTenantContextMiddleware>();
app.UseAntiforgery();

app.MapHealthCheckEndpoint();
app.MapWebhookEndpoints();
app.MapInternalOperationsEndpoints();
app.MapAlertWebhookEndpoints();
app.MapAdminAuthEndpoints();
app.MapAdminOperationsEndpoints();
app.MapMetricsEndpoints();
app.MapChannelQueueMetricsEndpoint();
app.MapGet("/", () => Results.Ok(new { status = "running", service = "MessengerWebhook" }));
app.MapFallbackToFile("/admin/{*path:nonfile}", "admin/index.html");

if (app.Environment.IsDevelopment())
    app.MapTestRagEndpoints();

app.Run();

// Expose Program to integration test assembly
public partial class Program { }
