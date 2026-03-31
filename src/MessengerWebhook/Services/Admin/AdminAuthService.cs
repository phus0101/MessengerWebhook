using System.Security.Claims;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Admin;

public class AdminAuthService : IAdminAuthService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly IPasswordHasher<ManagerProfile> _passwordHasher;
    private readonly AdminOptions _options;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        MessengerBotDbContext dbContext,
        IPasswordHasher<ManagerProfile> passwordHasher,
        IOptions<AdminOptions> options,
        ILogger<AdminAuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureBootstrapManagerAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapEmail) || string.IsNullOrWhiteSpace(_options.BootstrapPassword))
        {
            return;
        }

        var email = _options.BootstrapEmail.Trim();
        var manager = await _dbContext.ManagerProfiles
            .IgnoreQueryFilters()
            .Include(x => x.FacebookPageConfig)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (manager == null)
        {
            var pageConfig = await _dbContext.FacebookPageConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.DefaultManagerEmail == email, cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(manager.PasswordHash))
        {
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

        var user = new AdminUserContext(
            manager.Id,
            manager.TenantId.Value,
            manager.Email,
            manager.FullName,
            manager.FacebookPageConfig?.FacebookPageId);

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
}
