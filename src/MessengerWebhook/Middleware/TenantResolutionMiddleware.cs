using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Middleware;

public class TenantResolutionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly AdminOptions _adminOptions;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IHostEnvironment environment,
        IOptions<AdminOptions> adminOptions)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _adminOptions = adminOptions.Value;
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
                var webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(body, SerializerOptions);
                var pageId = webhookEvent?.Entry?.FirstOrDefault()?.Id;

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
                        var adoptedPageConfig = await TryAdoptUnknownDevelopmentPageAsync(dbContext, pageId, cancellationToken: context.RequestAborted);
                        tenantContext.Initialize(adoptedPageConfig?.TenantId, pageId, adoptedPageConfig?.DefaultManagerEmail);
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

    private async Task<FacebookPageConfig?> TryAdoptUnknownDevelopmentPageAsync(
        MessengerBotDbContext dbContext,
        string pageId,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() ||
            !_adminOptions.AllowTenantWideVisibilityInDevelopment ||
            string.IsNullOrWhiteSpace(_adminOptions.BootstrapEmail))
        {
            return null;
        }

        var bootstrapPageConfig = await dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.DefaultManagerEmail == _adminOptions.BootstrapEmail && x.IsActive, cancellationToken);
        if (bootstrapPageConfig?.TenantId == null)
        {
            return null;
        }

        var adoptedConfig = new FacebookPageConfig
        {
            TenantId = bootstrapPageConfig.TenantId,
            FacebookPageId = pageId,
            PageName = $"Imported Dev Page {pageId}",
            DefaultManagerEmail = _adminOptions.BootstrapEmail,
            IsPrimaryPage = false,
            IsActive = true
        };

        dbContext.FacebookPageConfigs.Add(adoptedConfig);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Adopted unknown Facebook page {PageId} into bootstrap tenant {TenantId} for development",
            pageId,
            bootstrapPageConfig.TenantId);

        return adoptedConfig;
    }
}
