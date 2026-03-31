namespace MessengerWebhook.Services.Support;

public interface ISupportCaseTokenService
{
    string GenerateToken(Guid caseId);
    bool ValidateToken(Guid caseId, string token);
}
