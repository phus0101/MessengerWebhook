namespace MessengerWebhook.Services.Metrics.Models;

// Frontend-compatible DTO matching TypeScript VariantComparison interface
public record VariantComparisonDto
{
    public required MetricsSummaryDto Control { get; init; }
    public required MetricsSummaryDto Treatment { get; init; }
    public bool StatisticalSignificance { get; init; }
    public decimal PValue { get; init; }
}
