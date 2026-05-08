namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Result of sub-intent classification
/// </summary>
public sealed record SubIntentResult
{
    /// <summary>Detected sub-intent category</summary>
    public required SubIntentCategory Category { get; init; }

    /// <summary>Confidence score (0.0-1.0)</summary>
    public required decimal Confidence { get; init; }

    /// <summary>Keywords that matched (for keyword detector)</summary>
    public string[] MatchedKeywords { get; init; } = Array.Empty<string>();

    /// <summary>Human-readable explanation (for AI classifier)</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>Source of classification (keyword, ai, hybrid)</summary>
    public string Source { get; init; } = "unknown";

    /// <summary>Timestamp of classification</summary>
    public DateTime ClassifiedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Create result with validation</summary>
    public static SubIntentResult Create(
        SubIntentCategory category,
        decimal confidence,
        string[] matchedKeywords,
        string explanation,
        string source)
    {
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be 0-1");

        return new SubIntentResult
        {
            Category = category,
            Confidence = confidence,
            MatchedKeywords = matchedKeywords ?? Array.Empty<string>(),
            Explanation = explanation ?? string.Empty,
            Source = source
        };
    }
}
