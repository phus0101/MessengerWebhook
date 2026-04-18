namespace MessengerWebhook.Services.ResponseValidation.Models;

/// <summary>
/// Result of response validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public List<ValidationIssue> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
