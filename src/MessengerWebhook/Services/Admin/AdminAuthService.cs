using System.Security.Claims;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Admin;

public class AdminAuthService : IAdminAuthService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IPasswordHasher<ManagerProfile> _passwordHasher;
    private readonly AdminOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        MessengerBotDbContext dbContext,
        IPasswordHasher<ManagerProfile> passwordHasher,
        IOptions<AdminOptions> options,
        IHostEnvironment environment,
        ILogger<AdminAuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task EnsureBootstrapManagerAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapEmail) || string.IsNullOrWhiteSpace(_options.BootstrapPassword))
        {
            return;
        }

        var email = _options.BootstrapEmail.Trim();
        var bootstrapPageConfig = await EnsureBootstrapWorkspaceAsync(email, cancellationToken);
        await AdoptDevelopmentOrphanDataAsync(bootstrapPageConfig, email, cancellationToken);
        var manager = await _dbContext.ManagerProfiles
            .IgnoreQueryFilters()
            .Include(x => x.FacebookPageConfig)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (manager == null)
        {
            var pageConfig = await _dbContext.FacebookPageConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.DefaultManagerEmail == email, cancellationToken);
            pageConfig ??= bootstrapPageConfig;
            var tenantId = pageConfig?.TenantId ?? await ResolveSingleTenantAsync(cancellationToken);
            if (tenantId == null)
            {
                _logger.LogWarning("Skipping bootstrap admin creation because no tenant could be resolved for {Email}", email);
                return;
            }

            manager = new ManagerProfile
            {
                TenantId = tenantId,
                FacebookPageConfigId = pageConfig?.Id,
                FullName = _options.BootstrapFullName,
                Email = email,
                IsPrimary = pageConfig == null
            };
            _dbContext.ManagerProfiles.Add(manager);
        }
        else if (manager.TenantId == null || manager.FacebookPageConfigId == null)
        {
            manager.TenantId ??= bootstrapPageConfig?.TenantId ?? await ResolveSingleTenantAsync(cancellationToken);
            manager.FacebookPageConfigId ??= bootstrapPageConfig?.Id;
            manager.UpdatedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(manager.PasswordHash))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        manager.PasswordHash = _passwordHasher.HashPassword(manager, _options.BootstrapPassword);
        manager.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminAuthResult> AuthenticateAsync(string email, string password, string? remoteIp, CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapManagerAsync(cancellationToken);
        var normalizedEmail = email.Trim();
        var manager = await _dbContext.ManagerProfiles
            .IgnoreQueryFilters()
            .Include(x => x.FacebookPageConfig)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.IsActive, cancellationToken);

        if (manager == null || manager.TenantId == null)
        {
            return new AdminAuthResult(false, "Email hoặc mật khẩu không đúng.", null);
        }

        if (manager.LockedUntil > DateTime.UtcNow)
        {
            return new AdminAuthResult(false, "Tài khoản đang bị khóa tạm thời.", null);
        }

        var passwordResult = string.IsNullOrWhiteSpace(manager.PasswordHash)
            ? PasswordVerificationResult.Failed
            : _passwordHasher.VerifyHashedPassword(manager, manager.PasswordHash, password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            manager.FailedLoginCount += 1;
            if (manager.FailedLoginCount >= 5)
            {
                manager.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            }

            manager.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new AdminAuthResult(false, "Email hoặc mật khẩu không đúng.", null);
        }

        manager.FailedLoginCount = 0;
        manager.LockedUntil = null;
        manager.LastLoginAt = DateTime.UtcNow;
        manager.LastLoginIp = remoteIp;
        manager.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var canAccessAllPagesInTenant = ShouldUseTenantWideVisibility(manager);
        var user = new AdminUserContext(
            manager.Id,
            manager.TenantId.Value,
            manager.Email,
            manager.FullName,
            manager.FacebookPageConfig?.FacebookPageId,
            canAccessAllPagesInTenant);

        _logger.LogInformation(
            "Admin user authenticated: {Email} with {VisibilityMode} visibility",
            manager.Email,
            user.VisibilityMode);

        return new AdminAuthResult(true, null, user, CreatePrincipal(user));
    }

    public ClaimsPrincipal CreatePrincipal(AdminUserContext user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.ManagerId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(AdminClaimTypes.ManagerId, user.ManagerId.ToString()),
            new(AdminClaimTypes.TenantId, user.TenantId.ToString()),
            new(AdminClaimTypes.FullName, user.FullName)
        };

        if (!string.IsNullOrWhiteSpace(user.FacebookPageId))
        {
            claims.Add(new Claim(AdminClaimTypes.FacebookPageId, user.FacebookPageId));
        }

        claims.Add(new Claim(AdminClaimTypes.TenantWideVisibility, user.CanAccessAllPagesInTenant.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "AdminCookie"));
    }

    public Task RecordLogoutAsync(AdminUserContext user, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin user logged out: {Email}", user.Email);
        return Task.CompletedTask;
    }

    private async Task<Guid?> ResolveSingleTenantAsync(CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return tenants.Count == 1 ? tenants[0] : null;
    }

    private async Task<FacebookPageConfig?> EnsureBootstrapWorkspaceAsync(string email, CancellationToken cancellationToken)
    {
        if (!_options.SeedDemoWorkspaceIfMissing)
        {
            return null;
        }

        var tenantCode = string.IsNullOrWhiteSpace(_options.BootstrapTenantCode)
            ? "mui-xu-dev"
            : _options.BootstrapTenantCode.Trim();
        var tenantName = string.IsNullOrWhiteSpace(_options.BootstrapTenantName)
            ? "Mui Xu Local Dev"
            : _options.BootstrapTenantName.Trim();
        var pageId = string.IsNullOrWhiteSpace(_options.BootstrapPageId)
            ? "DEV_PAGE_1"
            : _options.BootstrapPageId.Trim();
        var pageName = string.IsNullOrWhiteSpace(_options.BootstrapPageName)
            ? "Mui Xu Dev Page"
            : _options.BootstrapPageName.Trim();

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Code == tenantCode, cancellationToken);

        if (tenant == null)
        {
            tenant = new Tenant
            {
                Code = tenantCode,
                Name = tenantName,
                IsActive = true
            };
            _dbContext.Tenants.Add(tenant);
        }
        else
        {
            tenant.Name = tenantName;
            tenant.IsActive = true;
            tenant.UpdatedAt = DateTime.UtcNow;
        }

        var pageConfig = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.FacebookPageId == pageId, cancellationToken);

        if (pageConfig == null)
        {
            pageConfig = new FacebookPageConfig
            {
                TenantId = tenant.Id,
                FacebookPageId = pageId,
                PageName = pageName,
                DefaultManagerEmail = email,
                IsPrimaryPage = true,
                IsActive = true
            };
            _dbContext.FacebookPageConfigs.Add(pageConfig);
        }
        else
        {
            pageConfig.TenantId = tenant.Id;
            pageConfig.PageName = pageName;
            pageConfig.DefaultManagerEmail = email;
            pageConfig.IsPrimaryPage = true;
            pageConfig.IsActive = true;
            pageConfig.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return pageConfig;
    }

    private bool ShouldUseTenantWideVisibility(ManagerProfile manager)
    {
        if (!_environment.IsDevelopment() || !_options.AllowTenantWideVisibilityInDevelopment)
        {
            return false;
        }

        return string.Equals(
            manager.Email?.Trim(),
            _options.BootstrapEmail?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task AdoptDevelopmentOrphanDataAsync(
        FacebookPageConfig? bootstrapPageConfig,
        string bootstrapEmail,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() ||
            !_options.AllowTenantWideVisibilityInDevelopment ||
            bootstrapPageConfig?.TenantId == null)
        {
            return;
        }

        var tenantId = bootstrapPageConfig.TenantId.Value;
        var orphanPageIds = await _dbContext.DraftOrders
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null && x.FacebookPageId != null)
            .Select(x => x.FacebookPageId!)
            .Concat(_dbContext.HumanSupportCases
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == null && x.FacebookPageId != null)
                .Select(x => x.FacebookPageId!))
            .Concat(_dbContext.CustomerIdentities
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == null && x.FacebookPageId != null)
                .Select(x => x.FacebookPageId!))
            .Concat(_dbContext.BotConversationLocks
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == null && x.FacebookPageId != null)
                .Select(x => x.FacebookPageId!))
            .Distinct()
            .ToListAsync(cancellationToken);

        if (orphanPageIds.Count == 0 &&
            !await _dbContext.DraftOrders.IgnoreQueryFilters().AnyAsync(x => x.TenantId == null, cancellationToken) &&
            !await _dbContext.CustomerIdentities.IgnoreQueryFilters().AnyAsync(x => x.TenantId == null, cancellationToken) &&
            !await _dbContext.HumanSupportCases.IgnoreQueryFilters().AnyAsync(x => x.TenantId == null, cancellationToken) &&
            !await _dbContext.BotConversationLocks.IgnoreQueryFilters().AnyAsync(x => x.TenantId == null, cancellationToken))
        {
            return;
        }

        var existingPageIds = await _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .Where(x => orphanPageIds.Contains(x.FacebookPageId))
            .Select(x => x.FacebookPageId)
            .ToListAsync(cancellationToken);

        foreach (var pageId in orphanPageIds.Except(existingPageIds, StringComparer.OrdinalIgnoreCase))
        {
            _dbContext.FacebookPageConfigs.Add(new FacebookPageConfig
            {
                TenantId = tenantId,
                FacebookPageId = pageId,
                PageName = $"Imported Dev Page {pageId}",
                DefaultManagerEmail = bootstrapEmail,
                IsPrimaryPage = false,
                IsActive = true
            });
        }

        var orphanDrafts = await _dbContext.DraftOrders
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        foreach (var draft in orphanDrafts)
        {
            draft.TenantId = tenantId;
            draft.AssignedManagerEmail ??= bootstrapEmail;
            draft.UpdatedAt = DateTime.UtcNow;
        }

        var orphanCustomers = await _dbContext.CustomerIdentities
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        foreach (var customer in orphanCustomers)
        {
            customer.TenantId = tenantId;
            customer.UpdatedAt = DateTime.UtcNow;
        }

        var orphanCases = await _dbContext.HumanSupportCases
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        foreach (var supportCase in orphanCases)
        {
            supportCase.TenantId = tenantId;
            supportCase.AssignedToEmail ??= bootstrapEmail;
        }

        var orphanLocks = await _dbContext.BotConversationLocks
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        foreach (var botLock in orphanLocks)
        {
            botLock.TenantId = tenantId;
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Adopted development orphan data into tenant {TenantId} for bootstrap admin {Email}",
                tenantId,
                bootstrapEmail);
        }
    }
}
