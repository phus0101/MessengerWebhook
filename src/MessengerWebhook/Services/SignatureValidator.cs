using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MessengerWebhook.Configuration;

namespace MessengerWebhook.Services;

/// <summary>
/// Validates HMAC-SHA256 signatures from Facebook webhook requests
/// </summary>
public class SignatureValidator : ISignatureValidator
{
    private readonly string _appSecret;
    private readonly ILogger<SignatureValidator> _logger;

    public SignatureValidator(IOptions<FacebookOptions> options, ILogger<SignatureValidator> logger)
    {
        _appSecret = options.Value.AppSecret ?? throw new ArgumentNullException(nameof(options.Value.AppSecret));
        _logger = logger;
    }

    public Task<bool> ValidateAsync(string rawBody, string signature)
    {
        if (string.IsNullOrEmpty(rawBody))
        {
            _logger.LogWarning("Raw body is null or empty");
            return Task.FromResult(false);
        }

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Signature is null or empty");
            return Task.FromResult(false);
        }

        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Signature does not start with 'sha256='");
            return Task.FromResult(false);
        }

        var providedHash = signature.Substring(7).ToLower();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computedHashString = Convert.ToHexString(computedHash).ToLower();

        // Use constant-time comparison to prevent timing attacks
        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedHash),
            Encoding.UTF8.GetBytes(computedHashString));

        if (!isValid)
        {
            _logger.LogWarning("Signature validation failed");
        }

        return Task.FromResult(isValid);
    }
}
