namespace MessengerWebhook.Services.Support.EmailTemplates;

public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateSupportCaseAssignedEmail(
        Guid caseId,
        string customerPSID,
        string reason,
        string summary,
        DateTime createdAt,
        string completeUrl,
        string dashboardUrl)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        body {{ font-family: Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .header {{ background: #4267B2; color: white; padding: 20px; text-align: center; }}
        .header h2 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 20px; }}
        .info-box {{ background: #f8f9fa; padding: 15px; margin: 15px 0; border-left: 4px solid #4267B2; border-radius: 4px; }}
        .info-box strong {{ color: #333; }}
        .button-container {{ text-align: center; margin: 20px 0; }}
        .button {{ display: inline-block; background: #4267B2; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; margin: 10px 5px; font-weight: bold; }}
        .button:hover {{ background: #365899; }}
        .button.secondary {{ background: #6c757d; }}
        .button.secondary:hover {{ background: #5a6268; }}
        .footer {{ color: #666; font-size: 12px; padding: 15px 20px; background: #f8f9fa; border-top: 1px solid #dee2e6; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h2>🔔 Support Case Assigned</h2>
        </div>
        <div class=""content"">
            <p>Xin chào,</p>
            <p>Một khách hàng cần hỗ trợ. Vui lòng xem xét và xử lý case này.</p>

            <div class=""info-box"">
                <strong>Case ID:</strong> {caseId}<br>
                <strong>Customer PSID:</strong> {customerPSID}<br>
                <strong>Reason:</strong> {reason}<br>
                <strong>Summary:</strong> {summary}<br>
                <strong>Created:</strong> {createdAt:yyyy-MM-dd HH:mm:ss}
            </div>

            <div class=""button-container"">
                <a href=""{completeUrl}"" class=""button"">✓ Complete Case</a>
                <a href=""{dashboardUrl}"" class=""button secondary"">📊 View Dashboard</a>
            </div>
        </div>
        <div class=""footer"">
            <p>Clicking ""Complete Case"" will unlock the bot and allow it to resume conversations with this customer.</p>
            <p>This is an automated message from Múi Xù Messenger Bot System.</p>
        </div>
    </div>
</body>
</html>";
    }
}
