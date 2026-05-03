using System.Globalization;
using System.Text;

namespace MessengerWebhook.Services.Policy;

public sealed class DefaultPolicyMessageNormalizer : IPolicyMessageNormalizer
{
    public string Normalize(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var normalized = message.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character == '!')
            {
                builder.Append('i');
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(NormalizeLeetspeak(character));
                continue;
            }

            builder.Append(' ');
        }

        var result = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return CanonicalizeKnownPolicyTerms(result);
    }

    private static string CanonicalizeKnownPolicyTerms(string value)
    {
        return value
            .Replace("prompt injectiion", "prompt injection", StringComparison.OrdinalIgnoreCase)
            .Replace("prompt inject ion", "prompt injection", StringComparison.OrdinalIgnoreCase)
            .Replace("prompt inject on", "prompt injection", StringComparison.OrdinalIgnoreCase);
    }

    private static char NormalizeLeetspeak(char character)
    {
        return character switch
        {
            '0' => 'o',
            '1' => 'i',
            '!' => 'i',
            _ => char.ToLowerInvariant(character)
        };
    }
}
