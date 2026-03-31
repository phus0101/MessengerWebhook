using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Tenants;

namespace MessengerWebhook.Middleware;

public class AdminTenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public AdminTenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.Request.Path.StartsWithSegments("/admin") &&
            AdminUserContext.FromPrincipal(context.User) is { } user)
        {
            tenantContext.Initialize(
                user.TenantId,
                user.CanAccessAllPagesInTenant ? null : user.FacebookPageId,
                user.Email);
        }

        await _next(context);
    }
}
