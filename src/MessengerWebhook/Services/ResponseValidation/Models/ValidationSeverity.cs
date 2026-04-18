namespace MessengerWebhook.Services.ResponseValidation.Models;

/// <summary>
/// Severity level for validation issues
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational, no action needed
    /// </summary>
    Info,

    /// <summary>
    /// Minor issue, log but allow
    /// </summary>
    Warning,

    /// <summary>
    /// Major issue, should block
    /// </summary>
    Error,

    /// <summary>
    /// Critical issue, must block
    /// </summary>
    Critical
}
