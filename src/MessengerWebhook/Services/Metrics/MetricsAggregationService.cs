using MessengerWebhook.Data;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MessengerWebhook.Services.Metrics;

public class MetricsAggregationService : IMetricsAggregationService
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MetricsAggregationService> _logger;

    public MetricsAggregationService(
        MessengerBotDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<MetricsAggregationService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<MetricsSummaryDto> GetSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantContext.TenantId;
        if (resolvedTenantId == null)
        {
            throw new InvalidOperationException("TenantId is required");
        }

        var metrics = await _dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.TenantId == resolvedTenantId
                && m.CreatedAt >= startDate
                && m.CreatedAt < endDate)
            .ToListAsync(cancellationToken);

        if (!metrics.Any())
        {
            return new MetricsSummaryDto
            {
                TotalConversations = 0,
                CompletionRate = 0,
                EscalationRate = 0,
                AbandonmentRate = 0,
                AvgMessagesPerConversation = 0,
                AvgPipelineLatencyMs = 0
            };
        }

        var totalConversations = metrics.Select(m => m.SessionId).Distinct().Count();
        var avgMessagesPerConversation = totalConversations > 0
            ? (decimal)metrics.Count / totalConversations
            : 0;

        var sessionsWithOutcome = metrics
            .Where(m => !string.IsNullOrEmpty(m.ConversationOutcome))
            .GroupBy(m => m.SessionId)
            .Select(g => new { SessionId = g.Key, Outcome = g.Last().ConversationOutcome })
            .ToList();

        var totalSessionsWithOutcome = sessionsWithOutcome.Count;
        var completionRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "completed") / totalSessionsWithOutcome
            : 0;

        var escalationRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "escalated") / totalSessionsWithOutcome
            : 0;

        var abandonmentRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "abandoned") / totalSessionsWithOutcome
            : 0;

        var avgPipelineLatency = metrics.Any(m => m.PipelineLatencyMs.HasValue)
            ? (int)metrics.Where(m => m.PipelineLatencyMs.HasValue).Average(m => m.PipelineLatencyMs!.Value)
            : 0;

        return new MetricsSummaryDto
        {
            TotalConversations = totalConversations,
            CompletionRate = Math.Round(completionRate, 2),
            EscalationRate = Math.Round(escalationRate, 2),
            AbandonmentRate = Math.Round(abandonmentRate, 2),
            AvgMessagesPerConversation = Math.Round(avgMessagesPerConversation, 1),
            AvgPipelineLatencyMs = avgPipelineLatency
        };
    }

    public async Task<VariantComparisonDto> GetVariantComparisonAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantContext.TenantId;
        if (resolvedTenantId == null)
        {
            throw new InvalidOperationException("TenantId is required");
        }

        var metrics = await _dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.TenantId == resolvedTenantId
                && m.CreatedAt >= startDate
                && m.CreatedAt < endDate)
            .ToListAsync(cancellationToken);

        var controlMetrics = metrics.Where(m => m.ABTestVariant == "control").ToList();
        var treatmentMetrics = metrics.Where(m => m.ABTestVariant == "treatment").ToList();

        var controlSummary = CalculateMetricsSummary(controlMetrics);
        var treatmentSummary = CalculateMetricsSummary(treatmentMetrics);

        // Calculate statistical significance (simplified chi-square test)
        var (isSignificant, pValue) = CalculateStatisticalSignificance(controlMetrics, treatmentMetrics);

        return new VariantComparisonDto
        {
            Control = controlSummary,
            Treatment = treatmentSummary,
            StatisticalSignificance = isSignificant,
            PValue = pValue
        };
    }

    public async Task<PipelinePerformanceDto> GetPipelinePerformanceAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantContext.TenantId;
        if (resolvedTenantId == null)
        {
            throw new InvalidOperationException("TenantId is required");
        }

        var metrics = await _dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.TenantId == resolvedTenantId
                && m.ABTestVariant == "treatment"
                && m.PipelineLatencyMs != null
                && m.CreatedAt >= startDate
                && m.CreatedAt < endDate)
            .ToListAsync(cancellationToken);

        // Filter AdditionalMetrics in memory (can't translate JSON column in SQL)
        metrics = metrics.Where(m => m.AdditionalMetrics != null).ToList();

        var latencies = new List<LatencyData>();

        foreach (var metric in metrics)
        {
            try
            {
                if (metric.AdditionalMetrics == null) continue;

                var root = metric.AdditionalMetrics.RootElement;

                var latency = new LatencyData
                {
                    EmotionDetection = GetJsonInt(root, "emotionDetectionMs"),
                    ToneMatching = GetJsonInt(root, "toneMatchingMs"),
                    ContextAnalysis = GetJsonInt(root, "contextAnalysisMs"),
                    SmallTalkDetection = GetJsonInt(root, "smallTalkDetectionMs"),
                    ResponseValidation = GetJsonInt(root, "responseValidationMs"),
                    Total = metric.PipelineLatencyMs ?? 0
                };

                latencies.Add(latency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse additional metrics for metric {MetricId}", metric.Id);
            }
        }

        if (!latencies.Any())
        {
            var emptyPercentiles = new LatencyPercentilesDto { P50 = 0, P95 = 0, P99 = 0 };
            return new PipelinePerformanceDto
            {
                Emotion = emptyPercentiles,
                Tone = emptyPercentiles,
                Context = emptyPercentiles,
                SmallTalk = emptyPercentiles,
                Validation = emptyPercentiles,
                Total = emptyPercentiles
            };
        }

        return new PipelinePerformanceDto
        {
            Emotion = CalculatePercentiles(latencies.Select(l => l.EmotionDetection).ToList()),
            Tone = CalculatePercentiles(latencies.Select(l => l.ToneMatching).ToList()),
            Context = CalculatePercentiles(latencies.Select(l => l.ContextAnalysis).ToList()),
            SmallTalk = CalculatePercentiles(latencies.Select(l => l.SmallTalkDetection).ToList()),
            Validation = CalculatePercentiles(latencies.Select(l => l.ResponseValidation).ToList()),
            Total = CalculatePercentiles(latencies.Select(l => l.Total).ToList())
        };
    }

    private MetricsSummaryDto CalculateMetricsSummary(List<Data.Entities.ConversationMetric> metrics)
    {
        if (!metrics.Any())
        {
            return new MetricsSummaryDto
            {
                TotalConversations = 0,
                CompletionRate = 0,
                EscalationRate = 0,
                AbandonmentRate = 0,
                AvgMessagesPerConversation = 0,
                AvgPipelineLatencyMs = 0
            };
        }

        var totalConversations = metrics.Select(m => m.SessionId).Distinct().Count();
        var avgMessagesPerConversation = totalConversations > 0
            ? (decimal)metrics.Count / totalConversations
            : 0;

        var sessionsWithOutcome = metrics
            .Where(m => !string.IsNullOrEmpty(m.ConversationOutcome))
            .GroupBy(m => m.SessionId)
            .Select(g => new { SessionId = g.Key, Outcome = g.Last().ConversationOutcome })
            .ToList();

        var totalSessionsWithOutcome = sessionsWithOutcome.Count;
        var completionRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "completed") / totalSessionsWithOutcome
            : 0;

        var escalationRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "escalated") / totalSessionsWithOutcome
            : 0;

        var abandonmentRate = totalSessionsWithOutcome > 0
            ? (decimal)sessionsWithOutcome.Count(s => s.Outcome == "abandoned") / totalSessionsWithOutcome
            : 0;

        var avgPipelineLatency = metrics.Any(m => m.PipelineLatencyMs.HasValue)
            ? (int)metrics.Where(m => m.PipelineLatencyMs.HasValue).Average(m => m.PipelineLatencyMs!.Value)
            : 0;

        return new MetricsSummaryDto
        {
            TotalConversations = totalConversations,
            CompletionRate = Math.Round(completionRate, 2),
            EscalationRate = Math.Round(escalationRate, 2),
            AbandonmentRate = Math.Round(abandonmentRate, 2),
            AvgMessagesPerConversation = Math.Round(avgMessagesPerConversation, 1),
            AvgPipelineLatencyMs = avgPipelineLatency
        };
    }

    private (bool isSignificant, decimal pValue) CalculateStatisticalSignificance(
        List<Data.Entities.ConversationMetric> controlMetrics,
        List<Data.Entities.ConversationMetric> treatmentMetrics)
    {
        // Simplified statistical significance calculation
        // For production, consider using a proper statistical library
        var controlSessions = controlMetrics.Select(m => m.SessionId).Distinct().Count();
        var treatmentSessions = treatmentMetrics.Select(m => m.SessionId).Distinct().Count();

        // Need at least 30 samples per group for meaningful statistics
        if (controlSessions < 30 || treatmentSessions < 30)
        {
            return (false, 1.0m);
        }

        var controlCompleted = controlMetrics
            .Where(m => m.ConversationOutcome == "completed")
            .Select(m => m.SessionId)
            .Distinct()
            .Count();

        var treatmentCompleted = treatmentMetrics
            .Where(m => m.ConversationOutcome == "completed")
            .Select(m => m.SessionId)
            .Distinct()
            .Count();

        var controlRate = controlSessions > 0 ? (double)controlCompleted / controlSessions : 0;
        var treatmentRate = treatmentSessions > 0 ? (double)treatmentCompleted / treatmentSessions : 0;

        // Simplified z-test for proportions
        var pooledRate = (double)(controlCompleted + treatmentCompleted) / (controlSessions + treatmentSessions);
        var se = Math.Sqrt(pooledRate * (1 - pooledRate) * (1.0 / controlSessions + 1.0 / treatmentSessions));

        if (se == 0)
        {
            return (false, 1.0m);
        }

        var zScore = Math.Abs(controlRate - treatmentRate) / se;

        // Approximate p-value from z-score (two-tailed test)
        // For z > 1.96, p < 0.05 (significant)
        var isSignificant = zScore > 1.96;
        var pValue = zScore > 1.96 ? 0.05m : 0.5m; // Simplified approximation

        return (isSignificant, pValue);
    }

    private LatencyPercentilesDto CalculatePercentiles(List<int> values)
    {
        if (!values.Any())
        {
            return new LatencyPercentilesDto { P50 = 0, P95 = 0, P99 = 0 };
        }

        var sorted = values.OrderBy(v => v).ToList();

        return new LatencyPercentilesDto
        {
            P50 = CalculatePercentile(sorted, 0.50),
            P95 = CalculatePercentile(sorted, 0.95),
            P99 = CalculatePercentile(sorted, 0.99)
        };
    }

    private int CalculatePercentile(List<int> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        double index = (sortedValues.Count - 1) * percentile;
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        // Linear interpolation
        double fraction = index - lowerIndex;
        return (int)(sortedValues[lowerIndex] * (1 - fraction) +
                     sortedValues[upperIndex] * fraction);
    }

    private int GetJsonInt(JsonElement root, string propertyName)
    {
        try
        {
            if (root.TryGetProperty(propertyName, out var property))
            {
                return property.GetInt32();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get property {PropertyName}", propertyName);
        }
        return 0;
    }

    public async Task<List<ConversationTrendDto>> GetConversationTrendsAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId ?? _tenantContext.TenantId;
        if (resolvedTenantId == null)
        {
            throw new InvalidOperationException("TenantId is required");
        }

        var metrics = await _dbContext.ConversationMetrics
            .AsNoTracking()
            .Where(m => m.TenantId == resolvedTenantId
                && m.CreatedAt >= startDate
                && m.CreatedAt < endDate)
            .ToListAsync(cancellationToken);

        // Group by date and calculate metrics for each day
        var dailyMetrics = metrics
            .GroupBy(m => m.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dayMetrics = g.ToList();
                var sessions = dayMetrics.Select(m => m.SessionId).Distinct().Count();
                var avgMessages = sessions > 0 ? (double)dayMetrics.Count / sessions : 0;

                var sessionsWithOutcome = dayMetrics
                    .Where(m => !string.IsNullOrEmpty(m.ConversationOutcome))
                    .GroupBy(m => m.SessionId)
                    .Select(sg => new { SessionId = sg.Key, Outcome = sg.Last().ConversationOutcome })
                    .ToList();

                var totalWithOutcome = sessionsWithOutcome.Count;
                var completionRate = totalWithOutcome > 0
                    ? (double)sessionsWithOutcome.Count(s => s.Outcome == "completed") / totalWithOutcome
                    : 0;

                var escalationRate = totalWithOutcome > 0
                    ? (double)sessionsWithOutcome.Count(s => s.Outcome == "escalated") / totalWithOutcome
                    : 0;

                var abandonmentRate = totalWithOutcome > 0
                    ? (double)sessionsWithOutcome.Count(s => s.Outcome == "abandoned") / totalWithOutcome
                    : 0;

                return new ConversationTrendDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    CompletionRate = Math.Round(completionRate, 2),
                    EscalationRate = Math.Round(escalationRate, 2),
                    AbandonmentRate = Math.Round(abandonmentRate, 2),
                    AvgMessages = Math.Round(avgMessages, 1)
                };
            })
            .ToList();

        return dailyMetrics;
    }

    private class LatencyData
    {
        public int EmotionDetection { get; set; }
        public int ToneMatching { get; set; }
        public int ContextAnalysis { get; set; }
        public int SmallTalkDetection { get; set; }
        public int ResponseValidation { get; set; }
        public int Total { get; set; }
    }
}
