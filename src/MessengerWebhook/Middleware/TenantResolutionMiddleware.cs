using System.Text.Json;
using MessengerWebhook.Data;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Middleware;

public class TenantResolutionMiddleware
{
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
        ITenantContext tenantContext)
    {
        if (context.Request.Method == "POST" && context.Request.Path == "/webhook")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            try
            {
                var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(body);
                var pageId = webhookEvent?.Entry.FirstOrDefault()?.Id;

                if (!string.IsNullOrWhiteSpace(pageId))
                {
                    var pageConfig = await dbContext.FacebookPageConfigs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.FacebookPageId == pageId && x.IsActive);

                    if (pageConfig?.TenantId != null)
                    {
                        tenantContext.Initialize(
                            pageConfig.TenantId.Value,
                            pageConfig.FacebookPageId,
                            pageConfig.DefaultManagerEmail);
                    }
                    else
                    {
                        tenantContext.Initialize(null, pageId, null);
                    }
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
