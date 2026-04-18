using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.ABTesting.Configuration;

public class ValidateABTestingOptions : IValidateOptions<ABTestingOptions>
{
    public ValidateOptionsResult Validate(string? name, ABTestingOptions options)
    {
        if (options.TreatmentPercentage < 0 || options.TreatmentPercentage > 100)
        {
            return ValidateOptionsResult.Fail(
                $"TreatmentPercentage must be between 0 and 100. Got: {options.TreatmentPercentage}");
        }

        if (string.IsNullOrWhiteSpace(options.HashSeed))
        {
            return ValidateOptionsResult.Fail("HashSeed cannot be empty");
        }

        return ValidateOptionsResult.Success;
    }
}
