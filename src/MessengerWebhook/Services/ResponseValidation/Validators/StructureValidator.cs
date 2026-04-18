using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.ResponseValidation.Models;

namespace MessengerWebhook.Services.ResponseValidation.Validators;

/// <summary>
/// Validates response structure and format
/// </summary>
public class StructureValidator
{
    public List<ValidationIssue> Validate(string response, ResponseValidationOptions options)
    {
        var issues = new List<ValidationIssue>();

        // Check minimum length
        if (response.Length < options.MinResponseLength)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "Structure",
                Message = $"Response too short: {response.Length} chars (min: {options.MinResponseLength})",
                SuggestedFix = "Provide more detailed response"
            });
        }

        // Check maximum length
        if (response.Length > options.MaxResponseLength)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "Structure",
                Message = $"Response too long: {response.Length} chars (max: {options.MaxResponseLength})",
                SuggestedFix = "Break into multiple messages or summarize"
            });
        }

        // Check for empty or whitespace-only
        if (string.IsNullOrWhiteSpace(response))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Critical,
                Category = "Structure",
                Message = "Response is empty or whitespace-only",
                SuggestedFix = "Generate meaningful response content"
            });
        }

        // Check for excessive line breaks
        var lineBreakCount = response.Count(c => c == '\n');
        if (lineBreakCount > 5)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Structure",
                Message = $"Excessive line breaks: {lineBreakCount}",
                SuggestedFix = "Use more compact formatting"
            });
        }

        return issues;
    }
}
