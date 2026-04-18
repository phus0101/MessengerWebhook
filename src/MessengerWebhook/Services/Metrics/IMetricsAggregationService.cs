using MessengerWebhook.Services.Metrics.Models;

namespace MessengerWebhook.Services.Metrics;

public interface IMetricsAggregationService
{
    Task<MetricsSummaryDto> GetSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<VariantComparisonDto> GetVariantComparisonAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<PipelinePerformanceDto> GetPipelinePerformanceAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<List<ConversationTrendDto>> GetConversationTrendsAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
