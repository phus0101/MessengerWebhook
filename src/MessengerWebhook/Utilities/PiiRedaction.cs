using System.Text.RegularExpressions;

namespace MessengerWebhook.Utilities;

/// <summary>
/// Masks PII (phone numbers, addresses) in log output for privacy compliance.
/// </summary>
public static class PiiRedaction
{
    // Vietnamese phone pattern: starts with 0, followed by 2 digits, then 4 digits, then 3 digits
    // Captures first 3 and last 3 digits: "091****5678" -> "091****78"
    private static readonly Regex PhoneRegex = new(
        @"\b(0\d{2})\d{4}(\d{3})\b",
        RegexOptions.Compiled);

    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return phone ?? string.Empty;

        return PhoneRegex.Replace(phone, "$1****$2");
    }

    public static string MaskAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return address ?? string.Empty;

        // Keep first word (street number) and last word (city/province), mask middle
        var parts = address.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return string.Join(" ", Enumerable.Repeat("****", parts.Length));

        return $"{parts[0]} **** {parts[^1]}";
    }

    /// <summary>
    /// Redacts PII from a string by scanning for phone patterns and masking them.
    /// Use for free-form text that may contain phone numbers.
    /// </summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return PhoneRegex.Replace(text, m => $"{m.Groups[1].Value}****{m.Groups[2].Value}");
    }
}
