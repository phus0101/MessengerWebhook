using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

public sealed class ValidateCohereOptions : IValidateOptions<CohereOptions>
{
    public ValidateOptionsResult Validate(string? name, CohereOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        // H4: warn at startup when enabled but key is missing
        var resolvedKey = string.IsNullOrEmpty(options.ApiKey)
            ? Environment.GetEnvironmentVariable("COHERE_API_KEY") ?? ""
            : options.ApiKey;

        if (string.IsNullOrWhiteSpace(resolvedKey))
            return ValidateOptionsResult.Fail("Cohere:ApiKey is required when Cohere:Enabled=true. Set COHERE_API_KEY env var or Cohere:ApiKey in config.");

        if (options.TimeoutMs < 500)
            return ValidateOptionsResult.Fail("Cohere:TimeoutMs must be at least 500ms.");

        if (options.CandidateMultiplier < 2)
            return ValidateOptionsResult.Fail("Cohere:CandidateMultiplier must be at least 2.");

        return ValidateOptionsResult.Success;
    }
}
