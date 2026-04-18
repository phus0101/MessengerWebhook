using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.ResponseValidation.Configuration;

/// <summary>
/// Validates ResponseValidationOptions configuration at startup
/// </summary>
public class ValidateResponseValidationOptions : IValidateOptions<ResponseValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, ResponseValidationOptions options)
    {
        var errors = new List<string>();

        if (options.MinResponseLength < 0)
            errors.Add("MinResponseLength must be >= 0");

        if (options.MaxResponseLength < options.MinResponseLength)
            errors.Add("MaxResponseLength must be >= MinResponseLength");

        if (options.MaxResponseLength > 10000)
            errors.Add("MaxResponseLength must be <= 10000");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
