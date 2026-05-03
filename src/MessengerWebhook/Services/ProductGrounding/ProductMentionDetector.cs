using System.Text.RegularExpressions;

namespace MessengerWebhook.Services.ProductGrounding;

public interface IProductMentionDetector
{
    IReadOnlyList<string> ExtractProductMentions(string text);
    bool ContainsProductMention(string text);
}

public partial class ProductMentionDetector : IProductMentionDetector
{
    private static readonly string[] CategoryTerms =
    {
        "mặt nạ", "mat na", "kem", "serum", "toner", "sữa rửa mặt", "sua rua mat", "chống nắng", "chong nang", "tẩy trang", "tay trang"
    };

    private static readonly string[] LeadingWords = { "dạ", "da", "bên", "ben", "dòng", "dong", "sản phẩm", "san pham", "mẫu", "mau", "loại", "loai" };

    private static readonly string[] CategoryStopWords =
    {
        "dưỡng", "duong", "cấp", "cap", "trị", "tri", "phục hồi", "phuc hoi", "làm sạch", "lam sach", "cho", "nào", "nao"
    };

    private static readonly string[] GenericContextStarts =
    {
        "chị ", "chi ", "bạn ", "ban ", "mình ", "minh ", "khách ", "khach ", "sản phẩm này", "san pham nay", "sản phẩm đó", "san pham do"
    };

    public IReadOnlyList<string> ExtractProductMentions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var mentions = CategoryProductNameRegex()
            .Matches(text)
            .Cast<Match>()
            .Concat(TitleCaseProductNameRegex().Matches(text).Cast<Match>())
            .Concat(ContextualProductNameRegex().Matches(text).Cast<Match>().Select(match => match.Groups["name"]))
            .Select(match => TrimCandidate(match.Value))
            .Where(IsSpecificProductMention)
            .ToList();

        return RemoveRedundantMentions(mentions);
    }

    public bool ContainsProductMention(string text)
    {
        return ExtractProductMentions(text).Count > 0;
    }

    public static string TrimCandidate(string value)
    {
        var candidate = value.Trim().Trim(',', '.', ':', ';', '!', '?', '*', '_', '"', '\'', '“', '”');
        candidate = RemoveLeadingWords(candidate);
        var stopWords = new[]
        {
            " giá", " gia", " có", " co", " giúp", " giup", " cấp", " cap", " là", " la",
            " chuyên", " rất", " rat", " tốt", " tot", " hợp", " hop", " phù hợp", " phu hop", " ạ", " nha", " nhé", " nhe"
        };

        foreach (var stopWord in stopWords)
        {
            var index = candidate.IndexOf(stopWord, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                candidate = candidate[..index].Trim();
            }
        }

        candidate = RemoveDuplicatedCategoryPrefix(candidate);
        return candidate.Trim().Trim(',', '.', ':', ';', '!', '?', '*', '_', '"', '\'', '“', '”');
    }

    private static IReadOnlyList<string> RemoveRedundantMentions(List<string> mentions)
    {
        var uniqueMentions = mentions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return uniqueMentions
            .Where(candidate => !uniqueMentions.Any(other =>
                !string.Equals(candidate, other, StringComparison.OrdinalIgnoreCase) &&
                other.Length > candidate.Length &&
                other.Contains(candidate, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string RemoveDuplicatedCategoryPrefix(string candidate)
    {
        foreach (var category in CategoryTerms.OrderByDescending(term => term.Length))
        {
            if (!candidate.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = candidate[category.Length..].TrimStart();
            if (remainder.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            {
                return remainder;
            }
        }

        return candidate;
    }

    private static string RemoveLeadingWords(string candidate)
    {
        foreach (var leadingWord in LeadingWords)
        {
            if (candidate.StartsWith(leadingWord + " ", StringComparison.OrdinalIgnoreCase))
            {
                return candidate[leadingWord.Length..].TrimStart();
            }
        }

        return candidate;
    }

    private static bool IsSpecificProductMention(string candidate)
    {
        if (candidate.Length == 0 || IsGenericCategoryNeed(candidate) || IsGenericContextPhrase(candidate))
        {
            return false;
        }

        return ContainsBrandLikeSignal(candidate) ||
               candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4;
    }

    private static bool ContainsBrandLikeSignal(string candidate)
    {
        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleCaseCount = words.Count(word => char.IsUpper(word[0]));
        return titleCaseCount >= 2 || candidate.Any(char.IsDigit) || candidate.Contains('-', StringComparison.Ordinal);
    }

    private static bool IsGenericContextPhrase(string candidate)
    {
        return GenericContextStarts.Any(start => candidate.StartsWith(start, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGenericCategoryNeed(string candidate)
    {
        foreach (var category in CategoryTerms.OrderByDescending(term => term.Length))
        {
            if (!candidate.StartsWith(category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = candidate[category.Length..].TrimStart();
            return remainder.Length == 0 || CategoryStopWords.Any(stopWord => remainder.StartsWith(stopWord, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    [GeneratedRegex(@"\b(?:mặt nạ|mat na|kem|serum|toner|sữa rửa mặt|sua rua mat|chống nắng|chong nang|tẩy trang|tay trang)\s+[\p{L}\p{M}0-9][\p{L}\p{M}0-9\s\-\.]*", RegexOptions.IgnoreCase)]
    private static partial Regex CategoryProductNameRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{M}0-9])\p{Lu}[\p{L}\p{M}0-9\-]*(?:\s+\p{Lu}[\p{L}\p{M}0-9\-]*){3,}(?![\p{L}\p{M}0-9])")]
    private static partial Regex TitleCaseProductNameRegex();

    [GeneratedRegex(@"(?:bên em có|ben em co|dòng|dong|sản phẩm|san pham|mẫu|mau|loại|loai|chị thử|chi thu|em gợi ý|em goi y|em recommend|nên dùng|nen dung|có thể dùng|co the dung|em nghĩ|em nghi)\s+(?<name>[\p{L}\p{M}0-9\-]+(?:\s+[\p{L}\p{M}0-9\-]+){3,})", RegexOptions.IgnoreCase)]
    private static partial Regex ContextualProductNameRegex();
}
