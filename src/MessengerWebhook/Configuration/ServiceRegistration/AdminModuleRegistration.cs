using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Knowledge;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class AdminModuleRegistration
{
    internal static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));

        services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                var adminOptions = configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
                options.Cookie.Name = adminOptions.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = adminOptions.LoginPath;
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/admin/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/admin/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminAuditService, AdminAuditService>();
        services.AddScoped<IAdminDashboardQueryService, AdminDashboardQueryService>();
        services.AddScoped<IAdminDraftOrderService, AdminDraftOrderService>();
        services.AddScoped<IKnowledgeImportService, KnowledgeImportService>();
        services.AddScoped<IPasswordHasher<ManagerProfile>, PasswordHasher<ManagerProfile>>();
        services.AddScoped<INobitaSubmissionService, NobitaSubmissionService>();

        return services;
    }
}
