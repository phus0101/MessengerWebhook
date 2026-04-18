using MessengerWebhook.Services.ResponseValidation.Models;

namespace MessengerWebhook.Services.ResponseValidation.Validators;

/// <summary>
/// Validates Vietnamese language quality in bot responses
/// </summary>
public class VietnameseQualityValidator
{
    private static readonly HashSet<string> CommonMixedLanguagePatterns = new()
    {
        "hi bạn", "hello shop", "thank you", "sorry", "ok bạn", "bye bye"
    };

    public List<ValidationIssue> Validate(string response)
    {
        var issues = new List<ValidationIssue>();

        // Check for mixed language (except product names)
        var lowerResponse = response.ToLower();
        foreach (var pattern in CommonMixedLanguagePatterns)
        {
            if (lowerResponse.Contains(pattern))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "Language",
                    Message = $"Mixed language detected: '{pattern}'",
                    SuggestedFix = "Use pure Vietnamese for better consistency"
                });
            }
        }

        // Check for excessive emoji
        var emojiCount = CountEmojis(response);
        if (emojiCount > 3)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Language",
                Message = $"Excessive emoji usage: {emojiCount} emojis",
                SuggestedFix = "Limit to 2-3 emojis per message for professionalism"
            });
        }

        // Check for all caps (shouting)
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allCapsWords = words.Where(w => w.Length > 2 && w.All(char.IsUpper)).ToList();
        if (allCapsWords.Count > 2)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Language",
                Message = $"Excessive all-caps words: {string.Join(", ", allCapsWords)}",
                SuggestedFix = "Use normal capitalization for better readability"
            });
        }

        return issues;
    }

    private static int CountEmojis(string text)
    {
        var count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // Check for surrogate pairs (most emojis are in supplementary planes)
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                var codePoint = char.ConvertToUtf32(c, text[i + 1]);

                // Common emoji ranges in supplementary planes
                if ((codePoint >= 0x1F600 && codePoint <= 0x1F64F) || // Emoticons
                    (codePoint >= 0x1F300 && codePoint <= 0x1F5FF) || // Misc Symbols and Pictographs
                    (codePoint >= 0x1F680 && codePoint <= 0x1F6FF) || // Transport and Map
                    (codePoint >= 0x1F900 && codePoint <= 0x1F9FF) || // Supplemental Symbols and Pictographs
                    (codePoint >= 0x1FA70 && codePoint <= 0x1FAFF))   // Symbols and Pictographs Extended-A
                {
                    count++;
                    i++; // Skip the low surrogate
                }
            }
            // Check BMP emoji ranges
            else if ((c >= 0x2600 && c <= 0x26FF) ||   // Misc symbols
                     (c >= 0x2700 && c <= 0x27BF) ||   // Dingbats
                     (c >= 0x2300 && c <= 0x23FF))     // Misc Technical
            {
                count++;
            }
        }
        return count;
    }
}
