namespace MessengerWebhook.Services.Metrics.Models;

public record MetricsSummary
{
    public DateRange Period { get; init; } = null!;
    public int TotalSessions { get; init; }
    public int TotalMessages { get; init; }
    public VariantStats Variants { get; init; } = null!;
    public ResponseTimeStats AvgResponseTimeMs { get; init; } = null!;
}

public record DateRange(DateTime Start, DateTime End);

public record VariantStats
{
    public VariantCount Control { get; init; } = null!;
    public VariantCount Treatment { get; init; } = null!;
}

public record VariantCount(int Sessions, int Messages);

public record ResponseTimeStats(int Control, int Treatment);
