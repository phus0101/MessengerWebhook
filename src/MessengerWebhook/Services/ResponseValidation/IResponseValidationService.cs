using MessengerWebhook.Services.ResponseValidation.Models;

namespace MessengerWebhook.Services.ResponseValidation;

/// <summary>
/// Service for validating bot responses before sending to customers
/// </summary>
public interface IResponseValidationService
{
    /// <summary>
    /// Validates a bot response against tone, context, and quality standards
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        ResponseValidationContext context,
        CancellationToken cancellationToken = default);
}
