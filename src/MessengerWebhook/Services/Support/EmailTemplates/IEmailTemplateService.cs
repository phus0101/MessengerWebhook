namespace MessengerWebhook.Services.Support.EmailTemplates;

public interface IEmailTemplateService
{
    string GenerateSupportCaseAssignedEmail(
        Guid caseId,
        string customerPSID,
        string reason,
        string summary,
        DateTime createdAt,
        string completeUrl,
        string dashboardUrl);
}
