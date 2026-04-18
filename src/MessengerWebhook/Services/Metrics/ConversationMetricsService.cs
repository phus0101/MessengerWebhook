using System.Collections.Concurrent;
using System.Text.Json;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Services.Metrics;

public class ConversationMetricsService : IConversationMetricsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConversationMetricsService> _logger;
    private readonly ConcurrentQueue<ConversationMetricData> _metricsBuffer;

    public ConversationMetricsService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConversationMetricsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _metricsBuffer = new ConcurrentQueue<ConversationMetricData>();
    }

    public Task LogAsync(ConversationMetricData metricData, CancellationToken cancellationToken = default)
    {
        // H1 Fix: Check buffer size limit to prevent OOM
        if (_metricsBuffer.Count >= 10000)
        {
            // Evict oldest item (FIFO)
            _metricsBuffer.TryDequeue(out _);
            _logger.LogWarning("Metrics buffer full (10000 items), evicting oldest metric");
        }

        // Non-blocking: just enqueue
        _metricsBuffer.Enqueue(metricData);

        _logger.LogDebug(
            "Metric enqueued - PSID: {PSID}, Variant: {Variant}, Buffer size: {BufferSize}",
            metricData.FacebookPSID,
            metricData.ABTestVariant,
            _metricsBuffer.Count);

        return Task.CompletedTask;
    }

    public int GetBufferSize() => _metricsBuffer.Count;

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = _metricsBuffer.Count;
        if (batchSize == 0)
        {
            return;
        }

        var batch = new List<ConversationMetricData>(batchSize);

        // Dequeue all items
        while (_metricsBuffer.TryDequeue(out var metric))
        {
            batch.Add(metric);
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

            var entities = batch.Select(m => new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                SessionId = m.SessionId,
                FacebookPSID = m.FacebookPSID,
                ABTestVariant = m.ABTestVariant,
                MessageTimestamp = m.MessageTimestamp,
                ConversationTurn = m.ConversationTurn,
                TotalResponseTimeMs = m.TotalResponseTimeMs,
                PipelineLatencyMs = m.PipelineLatencyMs,
                DetectedEmotion = m.DetectedEmotion,
                EmotionConfidence = m.EmotionConfidence,
                MatchedTone = m.MatchedTone,
                JourneyStage = m.JourneyStage,
                ValidationPassed = m.ValidationPassed,
                ValidationErrors = m.ValidationErrors != null
                    ? JsonDocument.Parse(JsonSerializer.Serialize(m.ValidationErrors))
                    : null,
                ConversationOutcome = m.ConversationOutcome,
                AdditionalMetrics = m.AdditionalMetrics != null
                    ? JsonDocument.Parse(JsonSerializer.Serialize(m.AdditionalMetrics))
                    : null,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await dbContext.ConversationMetrics.AddRangeAsync(entities, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Flushed {Count} metrics to database (Tenant: {TenantId})",
                entities.Count,
                tenantContext.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} metrics to database", batch.Count);

            // H2 Fix: Re-enqueue with retry tracking and 5-failure threshold
            var requeued = 0;
            var dropped = 0;

            foreach (var metric in batch)
            {
                metric.RetryCount++;

                if (metric.RetryCount <= 5)
                {
                    _metricsBuffer.Enqueue(metric);
                    requeued++;
                }
                else
                {
                    // Drop after 5 failures
                    dropped++;
                    _logger.LogWarning(
                        "Dropping metric after 5 failed retries - PSID: {PSID}, SessionId: {SessionId}",
                        metric.FacebookPSID,
                        metric.SessionId);
                }
            }

            _logger.LogInformation(
                "Retry handling: {Requeued} metrics re-enqueued, {Dropped} metrics dropped",
                requeued,
                dropped);

            // H2 Fix: Catch and log errors internally, do not propagate to caller
            // This prevents background flush failures from crashing the app
        }
    }
}
