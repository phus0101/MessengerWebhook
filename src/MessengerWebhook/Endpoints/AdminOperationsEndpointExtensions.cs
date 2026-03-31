using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Support;
using Microsoft.AspNetCore.Antiforgery;

namespace MessengerWebhook.Endpoints;

public static class AdminOperationsEndpointExtensions
{
    public static RouteGroupBuilder MapAdminOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api").RequireAuthorization();

        group.MapGet("/dashboard", async (
            HttpContext httpContext,
            IAdminDashboardQueryService dashboardQueryService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await dashboardQueryService.GetOverviewAsync(user, cancellationToken));
        });

        group.MapGet("/draft-orders", async (
            HttpContext httpContext,
            IAdminDashboardQueryService dashboardQueryService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await dashboardQueryService.GetDraftOrdersAsync(user, cancellationToken));
        });

        group.MapGet("/draft-orders/{id:guid}", async (
            Guid id,
            HttpContext httpContext,
            IAdminDashboardQueryService dashboardQueryService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var draft = await dashboardQueryService.GetDraftOrderAsync(user, id, cancellationToken);
            return draft == null ? Results.NotFound() : Results.Ok(draft);
        });

        group.MapPost("/draft-orders/{id:guid}/approve-submit", async (
            Guid id,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            INobitaSubmissionService nobitaSubmissionService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await nobitaSubmissionService.ApproveAndSubmitAsync(user, id, cancellationToken));
        });

        group.MapPost("/draft-orders/{id:guid}/reject", async (
            Guid id,
            DraftRejectRequest request,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            INobitaSubmissionService nobitaSubmissionService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await nobitaSubmissionService.RejectAsync(user, id, request.Notes, cancellationToken));
        });

        group.MapPost("/draft-orders/{id:guid}/retry-submit", async (
            Guid id,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            INobitaSubmissionService nobitaSubmissionService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await nobitaSubmissionService.RetrySubmitAsync(user, id, cancellationToken));
        });

        group.MapGet("/support-cases", async (HttpContext httpContext, IAdminDashboardQueryService dashboardQueryService, CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null ? Results.Unauthorized() : Results.Ok(await dashboardQueryService.GetSupportCasesAsync(user, cancellationToken));
        });

        group.MapGet("/support-cases/{id:guid}", async (Guid id, HttpContext httpContext, IAdminDashboardQueryService dashboardQueryService, CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var supportCase = await dashboardQueryService.GetSupportCaseAsync(user, id, cancellationToken);
            return supportCase == null ? Results.NotFound() : Results.Ok(supportCase);
        });

        group.MapPost("/support-cases/{id:guid}/claim", async (Guid id, HttpContext httpContext, IAntiforgery antiforgery, ISupportCaseManagementService supportCaseManagementService, CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var supportCase = await supportCaseManagementService.ClaimAsync(user, id, cancellationToken);
            return supportCase == null ? Results.NotFound() : Results.Ok(new { success = true });
        });

        group.MapPost("/support-cases/{id:guid}/resolve", async (Guid id, SupportCaseActionRequest request, HttpContext httpContext, IAntiforgery antiforgery, ISupportCaseManagementService supportCaseManagementService, CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var supportCase = await supportCaseManagementService.ResolveAsync(user, id, request.Notes, cancellationToken);
            return supportCase == null ? Results.NotFound() : Results.Ok(new { success = true });
        });

        group.MapPost("/support-cases/{id:guid}/cancel", async (Guid id, SupportCaseActionRequest request, HttpContext httpContext, IAntiforgery antiforgery, ISupportCaseManagementService supportCaseManagementService, CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var supportCase = await supportCaseManagementService.CancelAsync(user, id, request.Notes, cancellationToken);
            return supportCase == null ? Results.NotFound() : Results.Ok(new { success = true });
        });

        group.MapGet("/product-mappings", async (string? search, HttpContext httpContext, IAdminDashboardQueryService dashboardQueryService, CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null ? Results.Unauthorized() : Results.Ok(await dashboardQueryService.GetProductMappingsAsync(user, search, cancellationToken));
        });

        group.MapPost("/product-mappings/{productId}", async (string productId, UpdateProductMappingRequest request, HttpContext httpContext, IAntiforgery antiforgery, INobitaSubmissionService nobitaSubmissionService, CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null ? Results.Unauthorized() : Results.Ok(await nobitaSubmissionService.UpdateProductMappingAsync(user, productId, request.NobitaProductId, request.NobitaWeight, cancellationToken));
        });

        group.MapGet("/nobita/products", async (string? search, HttpContext httpContext, IAdminDashboardQueryService dashboardQueryService, CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null ? Results.Unauthorized() : Results.Ok(await dashboardQueryService.SearchNobitaProductsAsync(search, cancellationToken));
        });

        group.MapPost("/nobita/products/sync", async (SyncNobitaProductsRequest request, HttpContext httpContext, IAntiforgery antiforgery, INobitaSubmissionService nobitaSubmissionService, CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null ? Results.Unauthorized() : Results.Ok(await nobitaSubmissionService.SyncProductsAsync(user, request.Search, cancellationToken));
        });

        return group;
    }

    public sealed record DraftRejectRequest(string? Notes);
    public sealed record SupportCaseActionRequest(string? Notes);
    public sealed record UpdateProductMappingRequest(int NobitaProductId, decimal NobitaWeight);
    public sealed record SyncNobitaProductsRequest(string? Search);
}
