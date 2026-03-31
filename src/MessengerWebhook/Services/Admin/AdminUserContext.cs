using System.Security.Claims;

namespace MessengerWebhook.Services.Admin;

public sealed record AdminUserContext(
    Guid ManagerId,
    Guid TenantId,
    string Email,
    string FullName,
    string? FacebookPageId)
{
    public static AdminUserContext? FromPrincipal(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var managerId = principal.FindFirstValue(AdminClaimTypes.ManagerId);
        var tenantId = principal.FindFirstValue(AdminClaimTypes.TenantId);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var fullName = principal.FindFirstValue(AdminClaimTypes.FullName) ?? principal.Identity.Name;

        if (!Guid.TryParse(managerId, out var parsedManagerId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        return new AdminUserContext(
            parsedManagerId,
            parsedTenantId,
            email,
            fullName,
            principal.FindFirstValue(AdminClaimTypes.FacebookPageId));
    }
}
