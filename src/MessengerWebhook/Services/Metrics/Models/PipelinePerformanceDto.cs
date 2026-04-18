namespace MessengerWebhook.Services.Metrics.Models;

// Frontend-compatible DTO matching TypeScript PipelineLatency interface
public record PipelinePerformanceDto
{
    public required LatencyPercentilesDto Emotion { get; init; }
    public required LatencyPercentilesDto Tone { get; init; }
    public required LatencyPercentilesDto Context { get; init; }
    public required LatencyPercentilesDto SmallTalk { get; init; }
    public required LatencyPercentilesDto Validation { get; init; }
    public required LatencyPercentilesDto Total { get; init; }
}

public record LatencyPercentilesDto
{
    public int P50 { get; init; }
    public int P95 { get; init; }
    public int P99 { get; init; }
}
