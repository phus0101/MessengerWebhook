using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Middleware;

/// <summary>
/// Resolves tenant context from webhook payload based on Facebook Page ID.
/// Uses <see cref="FacebookPageConfigLookupService"/> for race-condition-safe page config creation.
/// </summary>
public class TenantResolutionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        FacebookPageConfigLookupService lookupService)
    {
        if (context.Request.Method == "POST" && context.Request.Path == "/webhook")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            try
            {
                var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(body, SerializerOptions);
                var pageId = webhookEvent?.Entry?.FirstOrDefault()?.Id;

                if (!string.IsNullOrWhiteSpace(pageId))
                {
                    // Use consolidated service that handles lookup + race-safe auto-adoption
                    var resolvedPageConfig = await lookupService.EnsurePageConfigAsync(pageId, context.RequestAborted);
                    tenantContext.Initialize(resolvedPageConfig?.TenantId, pageId, resolvedPageConfig?.DefaultManagerEmail);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to resolve tenant context from webhook payload");
            }
        }

        await _next(context);
    }
}
