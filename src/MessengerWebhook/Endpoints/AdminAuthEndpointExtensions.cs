using System.Security.Claims;
using MessengerWebhook.Services.Admin;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MessengerWebhook.Endpoints;

public static class AdminAuthEndpointExtensions
{
    public static RouteGroupBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/auth");

        group.MapGet("/me", (HttpContext httpContext, IAntiforgery antiforgery) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new
            {
                authenticated = user != null,
                antiForgeryToken = tokens.RequestToken,
                user = user == null
                    ? null
                    : new
                    {
                        user.ManagerId,
                        user.Email,
                        user.FullName,
                        user.TenantId,
                        user.FacebookPageId
                    }
            });
        });

        group.MapPost("/login", async (
            AdminLoginRequest request,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IAdminAuthService adminAuthService,
            IAdminAuditService adminAuditService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null)
            {
                return antiForgeryError;
            }

            var result = await adminAuthService.AuthenticateAsync(
                request.Email,
                request.Password,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
            if (!result.Succeeded || result.User == null || result.Principal == null)
            {
                return Results.BadRequest(new { error = result.Error ?? "Login failed." });
            }

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                result.Principal,
                new AuthenticationProperties { IsPersistent = request.RememberMe });

            await adminAuditService.LogAsync(result.User, "login", "admin-session", result.User.ManagerId.ToString(), null, cancellationToken);
            return Results.Ok(new { success = true });
        });

        group.MapPost("/logout", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IAdminAuthService adminAuthService,
            IAdminAuditService adminAuditService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null)
            {
                return antiForgeryError;
            }

            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user != null)
            {
                await adminAuthService.RecordLogoutAsync(user, cancellationToken);
                await adminAuditService.LogAsync(user, "logout", "admin-session", user.ManagerId.ToString(), null, cancellationToken);
            }

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { success = true });
        }).RequireAuthorization();

        return group;
    }

    public sealed record AdminLoginRequest(string Email, string Password, bool RememberMe = true);
}
