using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

/// <summary>
/// Validates WebhookOptions at startup. Fails if VerifyToken is missing.
/// </summary>
public class ValidateWebhookOptions : IValidateOptions<WebhookOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.VerifyToken))
        {
            return ValidateOptionsResult.Fail("Webhook:VerifyToken is required. Configure via User Secrets or environment variables.");
        }

        return ValidateOptionsResult.Success;
    }
}
