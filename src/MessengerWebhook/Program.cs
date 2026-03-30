using System.Threading.Channels;
using MessengerWebhook.BackgroundServices;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Middleware;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.AI.Strategies;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.QuickReply;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Configure strongly-typed options
builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection(FacebookOptions.SectionName));
builder.Services.Configure<WebhookOptions>(
    builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

// Configure PostgreSQL DbContext
builder.Services.AddDbContext<MessengerBotDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<MessengerWebhook.HealthChecks.ChannelHealthCheck>("channel_queue")
    .AddCheck<MessengerWebhook.HealthChecks.GraphApiHealthCheck>("graph_api");

// Configure JSON serializer for case-insensitive property matching
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Add memory cache for idempotency with size limit
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100_000; // Limit to 100k entries
    options.CompactionPercentage = 0.25; // Evict 25% when full
});

// Register services
builder.Services.AddSingleton<ISignatureValidator, SignatureValidator>();
builder.Services.AddScoped<WebhookProcessor>();

// Register repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISkinProfileRepository, SkinProfileRepository>();
builder.Services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
builder.Services.AddScoped<IIngredientCompatibilityRepository, IngredientCompatibilityRepository>();
builder.Services.AddScoped<IVectorSearchRepository, VectorSearchRepository>();
builder.Services.AddScoped<IGiftRepository, GiftRepository>();
builder.Services.AddScoped<IProductGiftMappingRepository, ProductGiftMappingRepository>();

// Register session manager
builder.Services.AddScoped<ISessionManager, SessionManager>();

// Register Phase 1 services (Quick Reply Handler)
builder.Services.AddScoped<IProductMappingService, ProductMappingService>();
builder.Services.AddScoped<IGiftSelectionService, GiftSelectionService>();
builder.Services.AddScoped<IFreeshipCalculator, FreeshipCalculator>();
builder.Services.AddScoped<IQuickReplyHandler, QuickReplyHandler>();

// Register state machine
builder.Services.AddScoped<IStateMachine, ConversationStateMachine>();

// Register state handlers
builder.Services.AddScoped<IStateHandler, IdleStateHandler>();
builder.Services.AddScoped<IStateHandler, GreetingStateHandler>();
builder.Services.AddScoped<IStateHandler, MainMenuStateHandler>();
builder.Services.AddScoped<IStateHandler, BrowsingProductsStateHandler>();
builder.Services.AddScoped<IStateHandler, ProductDetailStateHandler>();
builder.Services.AddScoped<IStateHandler, VariantSelectionStateHandler>();
builder.Services.AddScoped<IStateHandler, AddToCartStateHandler>();
builder.Services.AddScoped<IStateHandler, CartReviewStateHandler>();
builder.Services.AddScoped<IStateHandler, ShippingAddressStateHandler>();
builder.Services.AddScoped<IStateHandler, PaymentMethodStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderConfirmationStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderPlacedStateHandler>();
builder.Services.AddScoped<IStateHandler, OrderTrackingStateHandler>();
builder.Services.AddScoped<IStateHandler, SkinAnalysisStateHandler>();
builder.Services.AddScoped<IStateHandler, SkinConsultationStateHandler>();
builder.Services.AddScoped<IStateHandler, HelpStateHandler>();
builder.Services.AddScoped<IStateHandler, ErrorStateHandler>();

// Register AI strategies
builder.Services.AddSingleton<IModelSelectionStrategy, HybridModelSelectionStrategy>();

// Register Gemini handlers
builder.Services.AddTransient<GeminiAuthHandler>();
builder.Services.AddTransient<GeminiRetryHandler>();

// Configure HttpClient for GeminiService with handlers
builder.Services.AddHttpClient<IGeminiService, GeminiService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
.AddHttpMessageHandler<GeminiAuthHandler>()
.AddHttpMessageHandler<GeminiRetryHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Configure HttpClient for EmbeddingService (reuse same handlers)
builder.Services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
.AddHttpMessageHandler<GeminiAuthHandler>()
.AddHttpMessageHandler<GeminiRetryHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Configure HttpClient for MessengerService with Polly resilience
builder.Services.AddHttpClient<IMessengerService, MessengerService>()
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

// Register background services
builder.Services.AddHostedService<WebhookProcessingService>();
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddHostedService<MessageCleanupService>();

// Configure Channel for async event processing
var channel = Channel.CreateBounded<MessagingEvent>(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest // Drop oldest to prevent blocking
    });
builder.Services.AddSingleton(channel);

var app = builder.Build();

// Auto-apply migrations on startup (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to apply database migrations");
        throw;
    }
}

// Validate critical configuration on startup
var facebookOpts = app.Services.GetRequiredService<IOptions<FacebookOptions>>().Value;
var webhookOpts = app.Services.GetRequiredService<IOptions<WebhookOptions>>().Value;

if (string.IsNullOrWhiteSpace(facebookOpts.AppSecret))
    throw new InvalidOperationException("Facebook:AppSecret is required. Configure via User Secrets or environment variables.");
if (string.IsNullOrWhiteSpace(facebookOpts.PageAccessToken))
    throw new InvalidOperationException("Facebook:PageAccessToken is required. Configure via User Secrets or environment variables.");
if (string.IsNullOrWhiteSpace(webhookOpts.VerifyToken))
    throw new InvalidOperationException("Webhook:VerifyToken is required. Configure via User Secrets or environment variables.");

var geminiOpts = app.Services.GetRequiredService<IOptions<GeminiOptions>>().Value;
if (string.IsNullOrWhiteSpace(geminiOpts.ApiKey))
    throw new InvalidOperationException("Gemini:ApiKey is required. Configure via User Secrets or environment variables.");

// Add signature validation middleware
app.UseMiddleware<SignatureValidationMiddleware>();

// Health check endpoint with detailed response
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

// Webhook verification endpoint (GET)
app.MapGet("/webhook", (
    [FromQuery(Name = "hub.mode")] string? mode,
    [FromQuery(Name = "hub.verify_token")] string? verifyToken,
    [FromQuery(Name = "hub.challenge")] string? challenge,
    IOptions<WebhookOptions> options,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(verifyToken) || string.IsNullOrEmpty(challenge))
    {
        logger.LogWarning("Webhook verification failed: Missing parameters");
        return Results.BadRequest("Missing required parameters");
    }

    if (mode != "subscribe")
    {
        logger.LogWarning("Webhook verification failed: Invalid mode {Mode}", mode);
        return Results.StatusCode(403);
    }

    if (verifyToken != options.Value.VerifyToken)
    {
        logger.LogWarning("Webhook verification failed: Invalid verify token");
        return Results.StatusCode(403);
    }

    logger.LogInformation("Webhook verified successfully");
    return Results.Text(challenge);
})
.WithName("VerifyWebhook");

// Webhook event endpoint (POST)
app.MapPost("/webhook", async (
    WebhookEvent webhookEvent,
    Channel<MessagingEvent> channel,
    ILogger<Program> logger) =>
{
    if (webhookEvent.Object != "page")
    {
        logger.LogWarning("Invalid object type: {Object}", webhookEvent.Object);
        return Results.NotFound();
    }

    var eventCount = 0;
    foreach (var entry in webhookEvent.Entry)
    {
        foreach (var messagingEvent in entry.Messaging)
        {
            await channel.Writer.WriteAsync(messagingEvent);
            eventCount++;
        }
    }

    logger.LogInformation("Webhook received: {EventCount} events queued", eventCount);
    return Results.Ok(new { status = "EVENT_RECEIVED" });
})
.WithName("ReceiveWebhook");

// Metrics endpoint
app.MapGet("/metrics", (Channel<MessagingEvent> channel) =>
{
    var queueDepth = channel.Reader.Count;
    return Results.Ok(new
    {
        queue_depth = queueDepth,
        queue_capacity = 1000,
        queue_utilization_percent = (queueDepth * 100.0 / 1000),
        timestamp = DateTimeOffset.UtcNow
    });
});

// Root endpoint
app.MapGet("/", () => Results.Ok(new {
    status = "running",
    service = "MessengerWebhook"
}));

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
