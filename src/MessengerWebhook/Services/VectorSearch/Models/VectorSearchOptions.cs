namespace MessengerWebhook.Services.VectorSearch.Models;

/// <summary>
/// Optional parameters that narrow a vector search query via Pinecone metadata filters.
/// All properties are nullable / have safe defaults so existing callers remain unaffected.
/// </summary>
public record VectorSearchOptions
{
    /// <summary>
    /// Restrict results to a specific content type (e.g. "product", "policy", "faq").
    /// Null means no content-type filter is applied.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Restrict results to a specific policy version (e.g. "v1", "v2").
    /// Null means no policy-version filter is applied.
    /// </summary>
    public string? PolicyVersion { get; init; }

    /// <summary>
    /// Channel visibility filter.
    /// "all" (default) skips the filter entirely.
    /// Any other value adds: OR(In(channel_visibility, [value, "all"]), Exists(channel_visibility, false))
    /// so that records tagged for a specific channel AND records without any tag are both returned.
    /// </summary>
    public string ChannelVisibility { get; init; } = "all";

    /// <summary>
    /// Locale filter (e.g. "vi", "en").
    /// Null means no locale filter is applied.
    /// </summary>
    public string? Locale { get; init; }
}
