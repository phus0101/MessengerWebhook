namespace MessengerWebhook.Services.ProductGrounding;

public record GroundedProductContext
{
    public GroundedProductContext(
        bool requiresGrounding,
        IReadOnlyList<GroundedProduct> allowedProducts,
        string fallbackReply)
        : this(requiresGrounding, allowedProducts, fallbackReply, Array.Empty<GroundedProduct>(), null)
    {
    }

    public GroundedProductContext(
        bool requiresGrounding,
        IReadOnlyList<GroundedProduct> allowedProducts,
        string fallbackReply,
        IReadOnlyList<GroundedProduct> relatedSuggestions,
        string? relatedSuggestionReply)
    {
        RequiresGrounding = requiresGrounding;
        AllowedProducts = allowedProducts;
        FallbackReply = fallbackReply;
        RelatedSuggestions = relatedSuggestions;
        RelatedSuggestionReply = relatedSuggestionReply;
    }

    public bool RequiresGrounding { get; init; }
    public IReadOnlyList<GroundedProduct> AllowedProducts { get; init; }
    public string FallbackReply { get; init; }
    public IReadOnlyList<GroundedProduct> RelatedSuggestions { get; init; }
    public string? RelatedSuggestionReply { get; init; }
    public bool HasAllowedProducts => AllowedProducts.Count > 0;
    public bool HasRelatedSuggestions => RelatedSuggestions.Count > 0;
}
