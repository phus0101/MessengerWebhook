using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.SmallTalk.Configuration;

/// <summary>
/// Validates SmallTalkOptions configuration at startup
/// </summary>
public class ValidateSmallTalkOptions : IValidateOptions<SmallTalkOptions>
{
    public ValidateOptionsResult Validate(string? name, SmallTalkOptions options)
    {
        var errors = new List<string>();

        if (options.SmallTalkConfidenceThreshold < 0.0 || options.SmallTalkConfidenceThreshold > 1.0)
            errors.Add("SmallTalkConfidenceThreshold must be between 0.0 and 1.0");

        if (options.MaxSmallTalkTurns < 1 || options.MaxSmallTalkTurns > 10)
            errors.Add("MaxSmallTalkTurns must be between 1 and 10");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
