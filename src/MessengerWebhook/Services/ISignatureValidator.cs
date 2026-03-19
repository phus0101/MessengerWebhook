namespace MessengerWebhook.Services;

/// <summary>
/// Validates HMAC-SHA256 signatures from Facebook webhook requests
/// </summary>
public interface ISignatureValidator
{
    /// <summary>
    /// Validates the signature against the raw request body
    /// </summary>
    /// <param name="rawBody">Raw request body as string</param>
    /// <param name="signature">X-Hub-Signature-256 header value</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    Task<bool> ValidateAsync(string rawBody, string signature);
}
