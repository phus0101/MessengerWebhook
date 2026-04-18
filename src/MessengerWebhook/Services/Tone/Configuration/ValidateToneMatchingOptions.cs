using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Tone.Configuration;

/// <summary>
/// Validates ToneMatchingOptions configuration at startup
/// </summary>
public class ValidateToneMatchingOptions : IValidateOptions<ToneMatchingOptions>
{
    public ValidateOptionsResult Validate(string? name, ToneMatchingOptions options)
    {
        var errors = new List<string>();

        if (options.FrustrationEscalationThreshold < 0.0 || options.FrustrationEscalationThreshold > 1.0)
            errors.Add("FrustrationEscalationThreshold must be between 0.0 and 1.0");

        if (options.CacheDurationMinutes < 0)
            errors.Add("CacheDurationMinutes must be >= 0");

        var validPronouns = new[] { "anh", "chị", "em", "bạn" };
        if (!validPronouns.Contains(options.DefaultPronoun))
            errors.Add($"DefaultPronoun must be one of: {string.Join(", ", validPronouns)}");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
