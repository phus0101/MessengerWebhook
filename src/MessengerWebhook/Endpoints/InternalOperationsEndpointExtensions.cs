using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Knowledge;
using MessengerWebhook.Services.Support;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Endpoints;

public static class InternalOperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapInternalOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal");

        group.MapGet("/draft-orders", async (MessengerBotDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var drafts = await dbContext.DraftOrders
                .AsNoTracking()
                .Include(x => x.Items)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .Select(x => new
                {
                    x.Id,
                    x.DraftCode,
                    x.CustomerName,
                    x.CustomerPhone,
                    x.ShippingAddress,
                    x.Status,
                    x.RiskLevel,
                    x.RequiresManualReview,
                    x.AssignedManagerEmail,
                    itemCount = x.Items.Count,
                    x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(drafts);
        });

        group.MapGet("/support-cases", async (MessengerBotDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var cases = await dbContext.HumanSupportCases
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .Select(x => new
                {
                    x.Id,
                    x.FacebookPSID,
                    x.Reason,
                    x.Status,
                    x.Summary,
                    x.AssignedToEmail,
                    x.CreatedAt,
                    x.ClaimedAt,
                    x.ResolvedAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(cases);
        });

        group.MapPost("/support-cases/{id:guid}/complete", async (
            Guid id,
            CompleteSupportCaseRequest request,
            MessengerBotDbContext dbContext,
            IBotLockService botLockService,
            CancellationToken cancellationToken) =>
        {
            var supportCase = await dbContext.HumanSupportCases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (supportCase == null)
            {
                return Results.NotFound();
            }

            supportCase.Status = SupportCaseStatus.Resolved;
            supportCase.ResolutionNotes = request.ResolutionNotes;
            supportCase.ResolvedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await botLockService.ReleaseAsync(supportCase.FacebookPSID, cancellationToken);

            return Results.Ok(new { supportCase.Id, supportCase.Status, supportCase.ResolvedAt });
        });

        group.MapPost("/knowledge/import", async (
            KnowledgeImportRequest request,
            IKnowledgeImportService knowledgeImportService,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await knowledgeImportService.ImportTextAsync(
                request.Category,
                request.SourceName,
                request.SourceType,
                request.Content,
                request.Publish,
                cancellationToken);

            return Results.Ok(new
            {
                snapshot.Id,
                snapshot.Category,
                snapshot.SourceName,
                snapshot.Version,
                snapshot.IsPublished,
                snapshot.PublishedAt
            });
        });

        return endpoints;
    }

    public sealed record CompleteSupportCaseRequest(string? ResolutionNotes);

    public sealed record KnowledgeImportRequest(
        KnowledgeCategory Category,
        string SourceName,
        string SourceType,
        string Content,
        bool Publish);
}
