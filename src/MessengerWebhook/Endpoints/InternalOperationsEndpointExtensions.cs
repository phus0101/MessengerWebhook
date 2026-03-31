using System.Text;
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

        group.MapGet("/support-cases/{id:guid}/complete", async (
            Guid id,
            string token,
            string? source,
            MessengerBotDbContext dbContext,
            IBotLockService botLockService,
            ISupportCaseTokenService tokenService,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            // Validate token
            if (!tokenService.ValidateToken(id, token))
            {
                logger.LogWarning("Invalid token for support case {CaseId}", id);
                return Results.Content(GenerateErrorHtml("Token không hợp lệ hoặc đã hết hạn"), "text/html");
            }

            var supportCase = await dbContext.HumanSupportCases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (supportCase == null)
            {
                logger.LogWarning("Support case {CaseId} not found", id);
                return Results.Content(GenerateErrorHtml("Không tìm thấy case này"), "text/html");
            }

            // Check if already resolved
            if (supportCase.Status == SupportCaseStatus.Resolved)
            {
                logger.LogInformation("Support case {CaseId} already resolved", id);
                return Results.Content(GenerateAlreadyResolvedHtml(supportCase), "text/html");
            }

            // Mark as resolved and unlock bot
            supportCase.Status = SupportCaseStatus.Resolved;
            supportCase.ResolutionNotes = $"Resolved via {source ?? "email"} link";
            supportCase.ResolvedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await botLockService.ReleaseAsync(supportCase.FacebookPSID, cancellationToken);

            logger.LogInformation("Support case {CaseId} resolved via {Source}", id, source ?? "email");
            return Results.Content(GenerateSuccessHtml(supportCase), "text/html");
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

    private static string GenerateSuccessHtml(HumanSupportCase supportCase)
    {
        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Case Đã Hoàn Thành</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; padding: 40px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .icon {{ text-align: center; font-size: 64px; margin-bottom: 20px; }}
        h1 {{ color: #10b981; text-align: center; margin: 0 0 10px 0; }}
        p {{ color: #6b7280; text-align: center; line-height: 1.6; }}
        .details {{ background: #f9fafb; border-radius: 6px; padding: 20px; margin: 20px 0; }}
        .detail-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #e5e7eb; }}
        .detail-row:last-child {{ border-bottom: none; }}
        .label {{ font-weight: 600; color: #374151; }}
        .value {{ color: #6b7280; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">✅</div>
        <h1>Case Đã Hoàn Thành</h1>
        <p>Support case đã được đánh dấu là đã giải quyết. Bot sẽ tự động mở khóa và tiếp tục phục vụ khách hàng.</p>
        <div class=""details"">
            <div class=""detail-row"">
                <span class=""label"">Case ID:</span>
                <span class=""value"">{supportCase.Id}</span>
            </div>
            <div class=""detail-row"">
                <span class=""label"">Customer PSID:</span>
                <span class=""value"">{supportCase.FacebookPSID}</span>
            </div>
            <div class=""detail-row"">
                <span class=""label"">Resolved At:</span>
                <span class=""value"">{supportCase.ResolvedAt:yyyy-MM-dd HH:mm:ss} UTC</span>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateErrorHtml(string errorMessage)
    {
        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Lỗi</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; padding: 40px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .icon {{ text-align: center; font-size: 64px; margin-bottom: 20px; }}
        h1 {{ color: #ef4444; text-align: center; margin: 0 0 10px 0; }}
        p {{ color: #6b7280; text-align: center; line-height: 1.6; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">❌</div>
        <h1>Có Lỗi Xảy Ra</h1>
        <p>{errorMessage}</p>
    </div>
</body>
</html>";
    }

    private static string GenerateAlreadyResolvedHtml(HumanSupportCase supportCase)
    {
        return $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Case Đã Được Giải Quyết</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; padding: 40px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .icon {{ text-align: center; font-size: 64px; margin-bottom: 20px; }}
        h1 {{ color: #f59e0b; text-align: center; margin: 0 0 10px 0; }}
        p {{ color: #6b7280; text-align: center; line-height: 1.6; }}
        .details {{ background: #f9fafb; border-radius: 6px; padding: 20px; margin: 20px 0; }}
        .detail-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #e5e7eb; }}
        .detail-row:last-child {{ border-bottom: none; }}
        .label {{ font-weight: 600; color: #374151; }}
        .value {{ color: #6b7280; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">ℹ️</div>
        <h1>Case Đã Được Giải Quyết Trước Đó</h1>
        <p>Support case này đã được đánh dấu là đã giải quyết rồi.</p>
        <div class=""details"">
            <div class=""detail-row"">
                <span class=""label"">Case ID:</span>
                <span class=""value"">{supportCase.Id}</span>
            </div>
            <div class=""detail-row"">
                <span class=""label"">Resolved At:</span>
                <span class=""value"">{supportCase.ResolvedAt:yyyy-MM-dd HH:mm:ss} UTC</span>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    public sealed record CompleteSupportCaseRequest(string? ResolutionNotes);

    public sealed record KnowledgeImportRequest(
        KnowledgeCategory Category,
        string SourceName,
        string SourceType,
        string Content,
        bool Publish);
}
