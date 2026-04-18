namespace MessengerWebhook.Services.Metrics.Models;

public record PipelinePerformance
{
    public LatencyBreakdown AvgLatencyMs { get; init; } = null!;
    public PercentileLatency P95LatencyMs { get; init; } = null!;
}

public record LatencyBreakdown
{
    public int? EmotionDetection { get; init; }
    public int? ToneMatching { get; init; }
    public int? ContextAnalysis { get; init; }
    public int? SmallTalkDetection { get; init; }
    public int? ResponseValidation { get; init; }
    public int Total { get; init; }
}

public record PercentileLatency(int Total);
