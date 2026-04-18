using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Conversation.Configuration;

/// <summary>
/// Validates ConversationAnalysisOptions configuration at startup
/// </summary>
public class ValidateConversationAnalysisOptions : IValidateOptions<ConversationAnalysisOptions>
{
    public ValidateOptionsResult Validate(string? name, ConversationAnalysisOptions options)
    {
        var errors = new List<string>();

        if (options.AnalysisWindowSize < 1 || options.AnalysisWindowSize > 100)
            errors.Add("AnalysisWindowSize must be between 1 and 100");

        if (options.BuyingSignalThreshold < 0.0 || options.BuyingSignalThreshold > 1.0)
            errors.Add("BuyingSignalThreshold must be between 0.0 and 1.0");

        if (options.RepeatQuestionThreshold < 0.0 || options.RepeatQuestionThreshold > 1.0)
            errors.Add("RepeatQuestionThreshold must be between 0.0 and 1.0");

        if (options.RepeatQuestionWindow < 1 || options.RepeatQuestionWindow > 20)
            errors.Add("RepeatQuestionWindow must be between 1 and 20");

        if (options.CacheDurationMinutes < 0)
            errors.Add("CacheDurationMinutes must be >= 0");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
