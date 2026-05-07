using System.Globalization;
using System.Text;

namespace MessengerWebhook.Services.ProductGrounding;

// Single source of truth for product-name equivalence checks.
// Used by sanitizer (ProductGroundingService) and validator (ResponseValidationService) — they MUST agree.
public static class ProductNameNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lowered = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(lowered.Length);

        foreach (var character in lowered)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character == 'đ' ? 'd' : character);
            }
        }

        var withoutMarks = builder.ToString().Normalize(NormalizationForm.FormC);
        return string.Join(' ', withoutMarks.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool Equivalent(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
