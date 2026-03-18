using MessengerWebhook.Configuration;
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

// Root endpoint
app.MapGet("/", () => Results.Ok(new {
    status = "running",
    service = "MessengerWebhook"
}));

app.Run();
