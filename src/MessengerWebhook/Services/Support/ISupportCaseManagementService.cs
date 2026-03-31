using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Admin;

namespace MessengerWebhook.Services.Support;

public interface ISupportCaseManagementService
{
    Task<HumanSupportCase?> ClaimAsync(AdminUserContext actor, Guid caseId, CancellationToken cancellationToken = default);
    Task<HumanSupportCase?> ResolveAsync(AdminUserContext actor, Guid caseId, string? resolutionNotes, CancellationToken cancellationToken = default);
    Task<HumanSupportCase?> CancelAsync(AdminUserContext actor, Guid caseId, string? resolutionNotes, CancellationToken cancellationToken = default);
}
