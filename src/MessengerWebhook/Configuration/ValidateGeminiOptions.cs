using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

public sealed class ValidateGeminiOptions : IValidateOptions<GeminiOptions>
{
    public ValidateOptionsResult Validate(string? name, GeminiOptions options)
    {
        var errors = new List<string>();

        if (options.AiDetectionTimeoutMs < 1)
        {
            errors.Add("Gemini:AiDetectionTimeoutMs must be greater than 0.");
        }

        if (options.TimeoutSeconds < 1)
        {
            errors.Add("Gemini:TimeoutSeconds must be greater than 0.");
        }

        if (options.MaxRetries < 0)
        {
            errors.Add("Gemini:MaxRetries must be greater than or equal to 0.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
