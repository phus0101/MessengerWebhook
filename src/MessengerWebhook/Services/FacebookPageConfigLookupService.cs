using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services;

/// <summary>
/// Consolidated service for FacebookPageConfig lookup/creation with race condition handling.
/// Replaces duplicate logic in middleware and WebhookProcessor.
/// </summary>
public class FacebookPageConfigLookupService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger<FacebookPageConfigLookupService> _logger;

    public FacebookPageConfigLookupService(
        MessengerBotDbContext dbContext,
        IHostEnvironment environment,
        IOptions<AdminOptions> adminOptions,
        ILogger<FacebookPageConfigLookupService> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ensures a FacebookPageConfig exists for the given page ID.
    /// If not found and in development, auto-creates with bootstrap tenant.
    /// Handles race conditions via unique index + retry pattern.
    /// </summary>
    public async Task<FacebookPageConfig?> EnsurePageConfigAsync(string pageId, CancellationToken cancellationToken = default)
    {
        // First, try to find existing config
        var config = await _dbContext.FacebookPageConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FacebookPageId == pageId && x.IsActive, cancellationToken);

        if (config != null)
        {
            return config;
        }

        // Try to auto-adopt in development
        return await TryAdoptUnknownDevelopmentPageAsync(pageId, cancellationToken);
    }

    private async Task<FacebookPageConfig?> TryAdoptUnknownDevelopmentPageAsync(
        string pageId,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() ||
            !_adminOptions.AllowTenantWideVisibilityInDevelopment ||
            string.IsNullOrWhiteSpace(_adminOptions.BootstrapEmail))
        {
            return null;
        }

        var bootstrapPageConfig = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.DefaultManagerEmail == _adminOptions.BootstrapEmail && x.IsActive, cancellationToken);

        if (bootstrapPageConfig?.TenantId == null)
        {
            return null;
        }

        // Check if another concurrent request already created it
        var existing = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FacebookPageId == pageId && x.IsActive, cancellationToken);

        if (existing != null)
        {
            return existing;
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

        try
        {
            _dbContext.FacebookPageConfigs.Add(adoptedConfig);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Adopted unknown Facebook page {PageId} into bootstrap tenant {TenantId} for development",
                pageId,
                bootstrapPageConfig.TenantId);

            return adoptedConfig;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another concurrent request created it - fetch and return
            _logger.LogInformation(
                "Concurrent page adoption detected for {PageId}, using existing config",
                pageId);

            return await _dbContext.FacebookPageConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.FacebookPageId == pageId && x.IsActive, cancellationToken);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException != null)
        {
            return ex.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                   ex.InnerException.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                   ex.InnerException.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
