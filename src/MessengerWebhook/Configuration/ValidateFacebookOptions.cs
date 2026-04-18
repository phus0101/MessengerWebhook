using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

/// <summary>
/// Validates FacebookOptions at startup to fail fast on missing required config.
/// </summary>
public class ValidateFacebookOptions : IValidateOptions<FacebookOptions>
{
    private readonly bool _isDevelopment;

    public ValidateFacebookOptions(IHostEnvironment environment)
    {
        _isDevelopment = environment.IsDevelopment();
    }

    public ValidateOptionsResult Validate(string? name, FacebookOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AppSecret))
        {
            var message = "Facebook:AppSecret is required. " +
                          "Configure it via User Secrets, environment variables, or appsettings.json.";

            // In development, allow startup to proceed but still validate
            // This matches the original behavior where commented-out checks were disabled for dev
            if (!_isDevelopment)
            {
                return ValidateOptionsResult.Fail(message);
            }
        }

        // PageAccessToken can be overridden per-page in DB, so don't fail here
        return ValidateOptionsResult.Success;
    }
}
