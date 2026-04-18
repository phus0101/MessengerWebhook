using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Emotion.Configuration;

/// <summary>
/// Validates EmotionDetectionOptions configuration at startup
/// </summary>
public class ValidateEmotionDetectionOptions : IValidateOptions<EmotionDetectionOptions>
{
    public ValidateOptionsResult Validate(string? name, EmotionDetectionOptions options)
    {
        var failures = new List<string>();

        if (options.ContextWindowSize < 1 || options.ContextWindowSize > 10)
        {
            failures.Add("ContextWindowSize must be between 1 and 10");
        }

        if (options.ConfidenceThreshold < 0.0 || options.ConfidenceThreshold > 1.0)
        {
            failures.Add("ConfidenceThreshold must be between 0.0 and 1.0");
        }

        if (options.CacheDurationMinutes < 0)
        {
            failures.Add("CacheDurationMinutes cannot be negative");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
