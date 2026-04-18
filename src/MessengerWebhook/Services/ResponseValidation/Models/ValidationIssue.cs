namespace MessengerWebhook.Services.ResponseValidation.Models;

/// <summary>
/// Represents a validation issue found in a response
/// </summary>
public class ValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
}
