using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Tone.Models;

namespace MessengerWebhook.Services.ResponseValidation.Validators;

/// <summary>
/// Validates tone consistency in bot responses
/// </summary>
public class ToneConsistencyValidator
{
    public List<ValidationIssue> Validate(string response, ToneProfile toneProfile)
    {
        var issues = new List<ValidationIssue>();

        // Check pronoun usage
        var expectedPronoun = toneProfile.PronounText;
        if (!string.IsNullOrEmpty(expectedPronoun) && !response.Contains(expectedPronoun))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Tone",
                Message = $"Expected pronoun '{expectedPronoun}' not found in response",
                SuggestedFix = $"Include '{expectedPronoun}' to match tone profile"
            });
        }

        // Check formality markers for Formal tone
        if (toneProfile.Level == ToneLevel.Formal)
        {
            if (!response.Contains("dạ") && !response.Contains("ạ") && !response.Contains("vâng"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "Tone",
                    Message = "Formal tone expected but no formality markers (dạ/ạ/vâng) found",
                    SuggestedFix = "Add formality markers like 'dạ' or 'ạ' at appropriate positions"
                });
            }
        }

        // Check casual markers for Casual tone
        if (toneProfile.Level == ToneLevel.Casual)
        {
            var hasFormalMarkers = response.Contains("dạ") || response.Contains("vâng") || response.Contains(" ạ");
            if (hasFormalMarkers)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "Tone",
                    Message = "Casual tone expected but formal markers (dạ/vâng/ạ) found",
                    SuggestedFix = "Remove formal markers for casual conversation"
                });
            }
        }

        return issues;
    }
}
