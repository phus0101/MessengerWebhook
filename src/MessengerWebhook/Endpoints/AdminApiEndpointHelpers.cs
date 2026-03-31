using MessengerWebhook.Services.Admin;
using Microsoft.AspNetCore.Antiforgery;

namespace MessengerWebhook.Endpoints;

internal static class AdminApiEndpointHelpers
{
    public static AdminUserContext? GetUser(HttpContext httpContext)
    {
        return AdminUserContext.FromPrincipal(httpContext.User);
    }

    public static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext httpContext, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "Anti-forgery token is invalid." });
        }
    }
}
