using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Admin;

public class AdminAuditService : IAdminAuditService
{
    private readonly MessengerBotDbContext _dbContext;

    public AdminAuditService(MessengerBotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(
        AdminUserContext actor,
        string action,
        string resourceType,
        string resourceId,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            TenantId = actor.TenantId,
            FacebookPageId = actor.FacebookPageId,
            ManagerProfileId = actor.ManagerId,
            ActorEmail = actor.Email,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Details = details
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
