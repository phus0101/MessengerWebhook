using System.Net;
using System.Net.Mail;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Support.EmailTemplates;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly IEmailTemplateService _templateService;
    private readonly ISupportCaseTokenService _tokenService;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        IEmailTemplateService templateService,
        ISupportCaseTokenService tokenService,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _templateService = templateService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task SendSupportCaseAssignedAsync(HumanSupportCase supportCase, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress) ||
            string.IsNullOrWhiteSpace(supportCase.AssignedToEmail))
        {
            _logger.LogInformation(
                "Skipping support case email for {CaseId}; SMTP config or assignee missing",
                supportCase.Id);
            return;
        }

        // Generate secure token for case completion link
        var token = _tokenService.GenerateToken(supportCase.Id);
        var completeUrl = $"{_options.BaseUrl}/internal/support-cases/{supportCase.Id}/complete?token={token}&source=email";
        var dashboardUrl = $"{_options.BaseUrl}/admin/support-cases/{supportCase.Id}";

        // Generate HTML email body
        var htmlBody = _templateService.GenerateSupportCaseAssignedEmail(
            supportCase.Id,
            supportCase.FacebookPSID,
            supportCase.Reason.ToString(),
            supportCase.Summary,
            supportCase.CreatedAt,
            completeUrl,
            dashboardUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = $"🔔 Support Case Assigned: {supportCase.Id}",
            Body = htmlBody,
            IsBodyHtml = _options.EnableHtmlEmails
        };
        message.To.Add(supportCase.AssignedToEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Username, _options.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);

        _logger.LogInformation(
            "Sent support case email to {Email} for case {CaseId}",
            supportCase.AssignedToEmail,
            supportCase.Id);
    }
}
