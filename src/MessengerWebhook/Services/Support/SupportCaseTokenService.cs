using System.Security.Cryptography;
using System.Text;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Support;

public class SupportCaseTokenService : ISupportCaseTokenService
{
    private readonly SupportOptions _options;
    private readonly ILogger<SupportCaseTokenService> _logger;

    public SupportCaseTokenService(
        IOptions<SupportOptions> options,
        ILogger<SupportCaseTokenService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GenerateToken(Guid caseId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = $"{caseId}:{timestamp}";
        var signature = ComputeHmac(data);
        return $"{data}:{signature}";
    }

    public bool ValidateToken(Guid caseId, string token)
    {
        try
        {
            var parts = token.Split(':');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid token format: expected 3 parts, got {Count}", parts.Length);
                return false;
            }

            if (!Guid.TryParse(parts[0], out var tokenCaseId))
            {
                _logger.LogWarning("Invalid token: case ID is not a valid GUID");
                return false;
            }

            if (!long.TryParse(parts[1], out var timestamp))
            {
                _logger.LogWarning("Invalid token: timestamp is not a valid number");
                return false;
            }

            var signature = parts[2];

            // Validate case ID
            if (tokenCaseId != caseId)
            {
                _logger.LogWarning("Token case ID mismatch: expected {Expected}, got {Actual}", caseId, tokenCaseId);
                return false;
            }

            // Validate expiration (7 days default)
            var expirationSeconds = _options.TokenExpirationDays * 24 * 60 * 60;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - timestamp > expirationSeconds)
            {
                _logger.LogWarning("Token expired: created {Timestamp}, now {Now}, expiration {Expiration}s",
                    timestamp, now, expirationSeconds);
                return false;
            }

            // Validate signature
            var data = $"{parts[0]}:{parts[1]}";
            var expectedSignature = ComputeHmac(data);
            if (signature != expectedSignature)
            {
                _logger.LogWarning("Token signature mismatch");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    private string ComputeHmac(string data)
    {
        var secret = _options.TokenSecret ?? throw new InvalidOperationException("TokenSecret not configured");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
