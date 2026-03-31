using System.Net;
using System.Net.Mail;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
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

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = $"[Messenger Bot] Support case {supportCase.Id}",
            Body = $"""
Support case: {supportCase.Id}
Reason: {supportCase.Reason}
Summary: {supportCase.Summary}
Page: {supportCase.FacebookPageId ?? "unknown"}
Draft order: {supportCase.DraftOrderId?.ToString() ?? "n/a"}

Please review this case in the admin dashboard.
"""
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
        await client.SendMailAsync(message);
    }
}
