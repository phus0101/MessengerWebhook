namespace MessengerWebhook.Services.ResponseValidation.Configuration;

/// <summary>
/// Configuration options for response validation
/// </summary>
public class ResponseValidationOptions
{
    public bool EnableValidation { get; set; } = true;
    public bool EnableToneValidation { get; set; } = true;
    public bool EnableContextValidation { get; set; } = true;
    public bool EnableLanguageValidation { get; set; } = true;
    public bool EnableStructureValidation { get; set; } = true;
    public int MinResponseLength { get; set; } = 10;
    public int MaxResponseLength { get; set; } = 500;
    public bool BlockOnErrors { get; set; } = false; // Log only by default
    public bool BlockOnValidationError { get; set; } = false; // Fail-safe: block response if validation crashes
}
