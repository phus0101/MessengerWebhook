using System.Security.Claims;

namespace MessengerWebhook.Services.Admin;

public interface IAdminAuthService
{
    Task EnsureBootstrapManagerAsync(CancellationToken cancellationToken = default);
    Task<AdminAuthResult> AuthenticateAsync(string email, string password, string? remoteIp, CancellationToken cancellationToken = default);
    ClaimsPrincipal CreatePrincipal(AdminUserContext user);
    Task RecordLogoutAsync(AdminUserContext user, CancellationToken cancellationToken = default);
}
