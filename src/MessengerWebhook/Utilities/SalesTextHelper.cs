using System.Globalization;
using System.Text;

namespace MessengerWebhook.Utilities;

/// <summary>
/// Shared Vietnamese text normalization utilities used by Sales services.
/// </summary>
internal static class SalesTextHelper
{
    /// <summary>
    /// Strips diacritics, lowercases, and replaces 'đ' with 'd' for fuzzy matching.
    /// </summary>
    public static string NormalizeForMatching(string input)
    {
        var decomposed = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer.Append(character switch
            {
                'đ' => 'd',
                'Đ' => 'd',
                _ => character
            });
        }

        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }
}
