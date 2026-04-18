namespace MessengerWebhook.Services.Metrics.Models;

// Frontend-compatible DTO matching TypeScript MetricsSummary interface
public record MetricsSummaryDto
{
    public int TotalConversations { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal EscalationRate { get; init; }
    public decimal AbandonmentRate { get; init; }
    public decimal AvgMessagesPerConversation { get; init; }
    public int AvgPipelineLatencyMs { get; init; }
}
