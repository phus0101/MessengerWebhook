using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MessengerWebhook.Services.Observability;

public static partial class PiiRedactor
{
    // Vietnamese phone pattern: 03x, 07x, 08x, 09x — 10 digits
    [GeneratedRegex(@"(?<!\d)(0[3-9]\d{8})(?!\d)")]
    private static partial Regex PhonePattern();

    // General address signals (Vietnamese keywords for street/ward/district/city)
    [GeneratedRegex(@"\b(số\s+\d+|đường\s+\S+|phường\s+\S+|quận\s+\S+|huyện\s+\S+|tỉnh\s+\S+|tp\.\s*\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();

    /// <summary>
    /// Deterministic 12-char hex hash of PSID — safe for log correlation without exposing raw ID.
    /// </summary>
    public static string HashPsid(string psid)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(psid));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Masks Vietnamese phone numbers: 0912345678 → 091***5678
    /// </summary>
    public static string MaskPhone(string input)
    {
        return PhonePattern().Replace(input, m =>
        {
            var phone = m.Value;
            return phone.Length >= 10
                ? phone[..3] + "***" + phone[^4..]
                : "***";
        });
    }

    /// <summary>
    /// Replaces address tokens with [address] placeholder.
    /// </summary>
    public static string RedactAddress(string input) =>
        AddressPattern().Replace(input, "[address]");

    /// <summary>
    /// Full redaction: phone masking + address redaction. Use on user-provided free text before logging.
    /// </summary>
    public static string Redact(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return RedactAddress(MaskPhone(input));
    }
}
