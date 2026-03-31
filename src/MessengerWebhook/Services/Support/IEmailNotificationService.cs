using MessengerWebhook.Data.Entities;

namespace MessengerWebhook.Services.Support;

public interface IEmailNotificationService
{
    Task SendSupportCaseAssignedAsync(HumanSupportCase supportCase, CancellationToken cancellationToken = default);
}
