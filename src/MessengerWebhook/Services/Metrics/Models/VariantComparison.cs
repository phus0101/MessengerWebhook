namespace MessengerWebhook.Services.Metrics.Models;

public record VariantComparison
{
    public VariantMetrics Control { get; init; } = null!;
    public VariantMetrics Treatment { get; init; } = null!;
}

public record VariantMetrics
{
    public int Sessions { get; init; }
    public decimal AvgMessagesPerSession { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal EscalationRate { get; init; }
    public decimal AbandonmentRate { get; init; }
    public int AvgResponseTimeMs { get; init; }

    // Treatment-only metrics (null for control)
    public decimal? EmotionAccuracy { get; init; }
    public decimal? ToneMatchingRate { get; init; }
    public decimal? ValidationPassRate { get; init; }
}
