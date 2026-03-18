namespace MessengerWebhook.Configuration;

/// <summary>
/// Facebook API configuration options
/// </summary>
public class FacebookOptions
{
    public const string SectionName = "Facebook";

    /// <summary>
    /// Facebook App Secret for signature validation
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Page Access Token for Send API
    /// </summary>
    public string PageAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Facebook Graph API version (e.g., "v21.0")
    /// </summary>
    public string ApiVersion { get; set; } = "v21.0";

    /// <summary>
    /// Facebook Graph API base URL
    /// </summary>
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
}
