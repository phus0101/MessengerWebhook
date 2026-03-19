using System.Threading.Channels;
using MessengerWebhook.BackgroundServices;
using MessengerWebhook.Configuration;
using MessengerWebhook.Middleware;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configure strongly-typed options
builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection(FacebookOptions.SectionName));
builder.Services.Configure<WebhookOptions>(
    builder.Configuration.GetSection(WebhookOptions.SectionName));

// Add health checks
builder.Services.AddHealthChecks();

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

// Register background service
builder.Services.AddHostedService<WebhookProcessingService>();

// Configure Channel for async event processing
var channel = Channel.CreateBounded<MessagingEvent>(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest // Drop oldest to prevent blocking
    });
builder.Services.AddSingleton(channel);

var app = builder.Build();

// Validate critical configuration on startup
var facebookOpts = app.Services.GetRequiredService<IOptions<FacebookOptions>>().Value;
var webhookOpts = app.Services.GetRequiredService<IOptions<WebhookOptions>>().Value;

if (string.IsNullOrWhiteSpace(facebookOpts.AppSecret))
    throw new InvalidOperationException("Facebook:AppSecret is required. Configure via User Secrets or environment variables.");
if (string.IsNullOrWhiteSpace(facebookOpts.PageAccessToken))
    throw new InvalidOperationException("Facebook:PageAccessToken is required. Configure via User Secrets or environment variables.");
if (string.IsNullOrWhiteSpace(webhookOpts.VerifyToken))
    throw new InvalidOperationException("Webhook:VerifyToken is required. Configure via User Secrets or environment variables.");

// Add signature validation middleware
app.UseMiddleware<SignatureValidationMiddleware>();

// Health check endpoint
app.MapHealthChecks("/health");

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

// Root endpoint
app.MapGet("/", () => Results.Ok(new {
    status = "running",
    service = "MessengerWebhook"
}));

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
