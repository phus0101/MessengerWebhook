using MessengerWebhook.Data;
using MessengerWebhook.Services.Admin;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/customers", async (
            string? query,
            HttpContext httpContext,
            IAdminDashboardQueryService dashboardQueryService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await dashboardQueryService.SearchCustomersAsync(user, query, cancellationToken));
        });

        group.MapPost("/draft-orders/{id:guid}/update", async (
            Guid id,
            UpdateDraftOrderRequest request,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IAdminDraftOrderService adminDraftOrderService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            return user == null
                ? Results.Unauthorized()
                : Results.Ok(await adminDraftOrderService.UpdateDraftOrderAsync(user, id, request, cancellationToken));
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

        group.MapGet("/bot-locks", async (
            HttpContext httpContext,
            IBotLockService botLockService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var locks = await botLockService.GetActiveLocksAsync(cancellationToken);
            return Results.Ok(locks);
        });

        group.MapGet("/bot-locks/{psid}", async (
            string psid,
            HttpContext httpContext,
            IBotLockService botLockService,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            var lockHistory = await botLockService.GetLockHistoryAsync(psid, cancellationToken);
            var isLocked = await botLockService.IsLockedAsync(psid, cancellationToken);
            return Results.Ok(new { isLocked, history = lockHistory });
        });

        group.MapPost("/bot-locks/{psid}/unlock", async (
            string psid,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IBotLockService botLockService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            await botLockService.ReleaseAsync(psid, cancellationToken);
            return Results.Ok(new { success = true });
        });

        group.MapPost("/bot-locks/{psid}/extend", async (
            string psid,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IBotLockService botLockService,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();
            await botLockService.ExtendLockAsync(psid, 60, cancellationToken); // Extend by 60 minutes
            return Results.Ok(new { success = true });
        });

        group.MapPost("/vector-search/index-all", async (
            HttpContext httpContext,
            IServiceScopeFactory scopeFactory,
            IIndexingProgressTracker progressTracker,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            // Check for active jobs
            var activeJobs = progressTracker.GetActiveJobs();
            if (activeJobs.Count > 0)
            {
                return Results.Conflict(new { error = "An indexing job is already running", jobId = activeJobs[0].JobId });
            }

            // Capture tenant ID from current user context
            var tenantId = user.TenantId;

            // Get product count first
            using var countScope = scopeFactory.CreateScope();
            var dbContext = countScope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var totalProducts = await dbContext.Products.CountAsync(cancellationToken);

            // Create job
            var jobId = progressTracker.CreateJob(totalProducts);

            // Run indexing in background scope to avoid blocking
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductEmbeddingPipeline>>();

                try
                {
                    logger.LogInformation("Starting background indexing job {JobId} for tenant {TenantId}", jobId, tenantId);

                    // Set tenant context for background task
                    var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                    tenantContext.Initialize(tenantId, null, null);

                    var pipeline = scope.ServiceProvider.GetRequiredService<ProductEmbeddingPipeline>();
                    await pipeline.IndexAllProductsAsync(jobId, cts.Token);

                    logger.LogInformation("Completed indexing all products to Pinecone for job {JobId}", jobId);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Indexing job {JobId} was cancelled", jobId);
                    progressTracker.FailJob(jobId, "Job was cancelled");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to index all products to Pinecone for job {JobId}. Error: {Message}", jobId, ex.Message);
                    progressTracker.FailJob(jobId, ex.Message);
                }
            }, cts.Token);

            return Results.Ok(new { success = true, jobId, message = "Indexing started in background" });
        });

        group.MapPost("/vector-search/index-product/{productId}", async (
            string productId,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            ProductEmbeddingPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            var antiForgeryError = await AdminApiEndpointHelpers.ValidateAntiforgeryAsync(httpContext, antiforgery);
            if (antiForgeryError != null) return antiForgeryError;
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            // Validate productId
            if (string.IsNullOrWhiteSpace(productId))
            {
                return Results.BadRequest(new { error = "Product ID cannot be empty" });
            }

            await pipeline.IndexProductAsync(productId, cancellationToken);
            return Results.Ok(new { success = true, productId });
        });

        group.MapGet("/vector-search/index-status/{jobId:guid}", (
            Guid jobId,
            HttpContext httpContext,
            IIndexingProgressTracker progressTracker) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var job = progressTracker.GetJob(jobId);
            if (job == null)
            {
                return Results.NotFound(new { error = "Job not found or expired" });
            }

            return Results.Ok(new
            {
                jobId = job.JobId,
                status = job.Status.ToString(),
                totalProducts = job.TotalProducts,
                indexedProducts = job.IndexedProducts,
                progressPercentage = job.ProgressPercentage,
                currentProductId = job.CurrentProductId,
                currentProductName = job.CurrentProductName,
                startedAt = job.StartedAt,
                completedAt = job.CompletedAt,
                errorMessage = job.ErrorMessage
            });
        });

        return group;
    }

    public sealed record DraftRejectRequest(string? Notes);
    public sealed record SupportCaseActionRequest(string? Notes);
    public sealed record UpdateProductMappingRequest(int NobitaProductId, decimal NobitaWeight);
    public sealed record SyncNobitaProductsRequest(string? Search);
}
