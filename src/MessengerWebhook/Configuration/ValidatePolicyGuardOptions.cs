using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

public sealed class ValidatePolicyGuardOptions : IValidateOptions<PolicyGuardOptions>
{
    private readonly GeminiOptions _geminiOptions;

    public ValidatePolicyGuardOptions()
        : this(Options.Create(new GeminiOptions()))
    {
    }

    public ValidatePolicyGuardOptions(IOptions<GeminiOptions> geminiOptions)
    {
        _geminiOptions = geminiOptions.Value;
    }

    public ValidateOptionsResult Validate(string? name, PolicyGuardOptions options)
    {
        var errors = new List<string>();

        ValidateUnitInterval(options.SemanticClassifierMinConfidence, nameof(options.SemanticClassifierMinConfidence), errors);
        ValidateUnitInterval(options.SafeReplyThreshold, nameof(options.SafeReplyThreshold), errors);
        ValidateUnitInterval(options.SoftEscalateThreshold, nameof(options.SoftEscalateThreshold), errors);
        ValidateUnitInterval(options.HardEscalateThreshold, nameof(options.HardEscalateThreshold), errors);
        ValidateUnitInterval(options.RepeatMentionBoost, nameof(options.RepeatMentionBoost), errors);
        ValidateUnitInterval(options.OpenSupportCaseBoost, nameof(options.OpenSupportCaseBoost), errors);
        ValidateUnitInterval(options.DraftOrderBoost, nameof(options.DraftOrderBoost), errors);

        if (options.SafeReplyThreshold > options.SoftEscalateThreshold)
        {
            errors.Add("PolicyGuard:SafeReplyThreshold must be less than or equal to SoftEscalateThreshold.");
        }

        if (options.SoftEscalateThreshold > options.HardEscalateThreshold)
        {
            errors.Add("PolicyGuard:SoftEscalateThreshold must be less than or equal to HardEscalateThreshold.");
        }

        if (options.MaxRecentTurns < 1)
        {
            errors.Add("PolicyGuard:MaxRecentTurns must be greater than 0.");
        }

        if (options.ClassifierTimeoutMs < 1)
        {
            errors.Add("PolicyGuard:ClassifierTimeoutMs must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(options.SafeReplyMessage))
        {
            errors.Add("PolicyGuard:SafeReplyMessage must not be empty.");
        }

        if (options.EnableSemanticClassifier && string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            errors.Add("PolicyGuard:EnableSemanticClassifier requires Gemini:ApiKey.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateUnitInterval(decimal value, string propertyName, List<string> errors)
    {
        if (value < 0m || value > 1m)
        {
            errors.Add($"PolicyGuard:{propertyName} must be between 0 and 1.");
        }
    }
}
