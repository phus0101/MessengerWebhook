using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MessengerWebhook.Services.Emotion.Models;

namespace MessengerWebhook.Services.Emotion;

/// <summary>
/// Rule-based emotion detector using keyword matching, punctuation analysis, and emoji detection.
/// Thread-safe: all methods are stateless and can be called concurrently.
/// </summary>
public class RuleBasedEmotionDetector
{
    private static readonly Regex MultipleExclamationRegex = new(@"!{2,}", RegexOptions.Compiled);
    private static readonly Regex MultipleQuestionRegex = new(@"\?{2,}", RegexOptions.Compiled);
    private static readonly Regex EllipsisRegex = new(@"\.{3,}", RegexOptions.Compiled);

    /// <summary>
    /// Detect emotion from a single message
    /// </summary>
    public EmotionScore DetectEmotion(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return CreateNeutralScore();
        }

        var normalizedMessage = NormalizeText(message);
        var scores = new Dictionary<EmotionType, double>();

        // Calculate scores for each emotion type
        scores[EmotionType.Positive] = CalculatePositiveScore(normalizedMessage, message);
        scores[EmotionType.Negative] = CalculateNegativeScore(normalizedMessage, message);
        scores[EmotionType.Frustrated] = CalculateFrustratedScore(normalizedMessage, message);
        scores[EmotionType.Excited] = CalculateExcitedScore(normalizedMessage, message);
        scores[EmotionType.Neutral] = CalculateNeutralScore(scores);

        // Find primary emotion
        var primaryEmotion = scores.OrderByDescending(x => x.Value).First().Key;
        var confidence = scores[primaryEmotion];

        return new EmotionScore
        {
            PrimaryEmotion = primaryEmotion,
            Scores = scores,
            Confidence = Math.Min(confidence, 1.0),
            DetectionMethod = "rule-based",
            Metadata = new Dictionary<string, object>
            {
                ["normalized_message"] = normalizedMessage,
                ["has_negation"] = HasNegation(normalizedMessage)
            }
        };
    }

    private double CalculatePositiveScore(string normalizedMessage, string originalMessage)
    {
        double score = 0.0;

        // Keyword matching (40% weight)
        var keywordMatches = CountKeywordMatches(normalizedMessage, EmotionKeywords.Positive);
        score += keywordMatches * 0.4;

        // Emoji detection (30% weight)
        var emojiCount = CountEmojis(originalMessage, EmotionKeywords.PositiveEmojis);
        score += emojiCount * 0.3;

        // Punctuation analysis (30% weight)
        score += AnalyzePositivePunctuation(originalMessage) * 0.3;

        // Handle negation
        if (HasNegation(normalizedMessage))
        {
            score *= 0.3; // Reduce positive score if negation present
        }

        return score;
    }

    private double CalculateNegativeScore(string normalizedMessage, string originalMessage)
    {
        double score = 0.0;

        // Keyword matching (40% weight)
        var keywordMatches = CountKeywordMatches(normalizedMessage, EmotionKeywords.Negative);
        score += keywordMatches * 0.4;

        // Emoji detection (30% weight)
        var emojiCount = CountEmojis(originalMessage, EmotionKeywords.NegativeEmojis);
        score += emojiCount * 0.3;

        // Punctuation analysis (30% weight)
        score += AnalyzeNegativePunctuation(originalMessage) * 0.3;

        // Boost score if negation + positive word (e.g., "không tốt")
        if (HasNegation(normalizedMessage) && ContainsAnyKeyword(normalizedMessage, EmotionKeywords.Positive))
        {
            score += 0.5;
        }

        return score;
    }

    private double CalculateFrustratedScore(string normalizedMessage, string originalMessage)
    {
        double score = 0.0;

        // Keyword matching (40% weight)
        var keywordMatches = CountKeywordMatches(normalizedMessage, EmotionKeywords.Frustrated);
        score += keywordMatches * 0.4;

        // Emoji detection (30% weight)
        var emojiCount = CountEmojis(originalMessage, EmotionKeywords.FrustratedEmojis);
        score += emojiCount * 0.3;

        // Punctuation analysis (30% weight) - multiple exclamation marks indicate frustration
        var exclamationCount = MultipleExclamationRegex.Matches(originalMessage).Count;
        score += Math.Min(exclamationCount * 0.3, 0.6);

        return score;
    }

    private double CalculateExcitedScore(string normalizedMessage, string originalMessage)
    {
        double score = 0.0;

        // Keyword matching (40% weight)
        var keywordMatches = CountKeywordMatches(normalizedMessage, EmotionKeywords.Excited);
        score += keywordMatches * 0.4;

        // Emoji detection (40% weight - increased from 30% to prioritize excited emojis)
        var emojiCount = CountEmojis(originalMessage, EmotionKeywords.ExcitedEmojis);
        score += emojiCount * 0.4;

        // Punctuation analysis (30% weight)
        score += AnalyzeExcitedPunctuation(originalMessage) * 0.3;

        return score;
    }

    private double CalculateNeutralScore(Dictionary<EmotionType, double> scores)
    {
        // Neutral score is inverse of other emotions
        var maxOtherScore = scores
            .Where(x => x.Key != EmotionType.Neutral)
            .Select(x => x.Value)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(0, 1.0 - maxOtherScore);
    }

    private int CountKeywordMatches(string message, HashSet<string> keywords)
    {
        int count = 0;

        // Check for phrase matches first (multi-word keywords)
        foreach (var keyword in keywords.Where(k => k.Contains(' ')))
        {
            var normalizedKeyword = NormalizeText(keyword);
            if (message.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        // Then check for single word matches
        var words = message.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        count += words.Count(word => keywords.Any(keyword => !keyword.Contains(' ') && NormalizeText(keyword) == word));

        return count;
    }

    private int CountEmojis(string message, HashSet<string> emojis)
    {
        return emojis.Count(emoji => message.Contains(emoji));
    }

    private bool ContainsAnyKeyword(string message, HashSet<string> keywords)
    {
        var words = message.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => keywords.Any(keyword => !keyword.Contains(' ') && NormalizeText(keyword) == word));
    }

    private bool HasNegation(string message)
    {
        var words = message.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => EmotionKeywords.Negations.Any(negation => NormalizeText(negation) == word));
    }

    private double AnalyzePositivePunctuation(string message)
    {
        double score = 0.0;

        // Multiple exclamation marks (strong positive)
        if (MultipleExclamationRegex.IsMatch(message))
        {
            var matches = MultipleExclamationRegex.Matches(message);
            score += Math.Min(matches.Count * 0.4, 0.8);
        }
        // Single exclamation mark (mild positive)
        else if (message.Contains('!'))
        {
            score += 0.3;
        }

        return score;
    }

    private double AnalyzeNegativePunctuation(string message)
    {
        double score = 0.0;

        // Multiple question marks (confusion/concern)
        if (MultipleQuestionRegex.IsMatch(message))
        {
            score += 0.2;
        }

        // Ellipsis (hesitation/disappointment)
        if (EllipsisRegex.IsMatch(message))
        {
            score += 0.2;
        }

        return score;
    }

    private double AnalyzeExcitedPunctuation(string message)
    {
        double score = 0.0;

        // Multiple exclamation marks (high excitement)
        var exclamationCount = MultipleExclamationRegex.Matches(message).Count;
        if (exclamationCount > 0)
        {
            score += Math.Min(exclamationCount * 0.4, 0.8);
        }

        return score;
    }

    private string NormalizeText(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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

    private EmotionScore CreateNeutralScore()
    {
        return new EmotionScore
        {
            PrimaryEmotion = EmotionType.Neutral,
            Scores = new Dictionary<EmotionType, double>
            {
                [EmotionType.Positive] = 0,
                [EmotionType.Neutral] = 1.0,
                [EmotionType.Negative] = 0,
                [EmotionType.Frustrated] = 0,
                [EmotionType.Excited] = 0
            },
            Confidence = 1.0,
            DetectionMethod = "rule-based"
        };
    }
}
