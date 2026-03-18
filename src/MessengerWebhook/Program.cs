using MessengerWebhook.Configuration;
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

// Root endpoint
app.MapGet("/", () => Results.Ok(new {
    status = "running",
    service = "MessengerWebhook"
}));

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
