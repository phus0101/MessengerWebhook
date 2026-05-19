# Phase 7: Logging & Monitoring

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Logging & Monitoring Best Practices

## Overview
- **Priority:** P1 (High)
- **Status:** Pending
- **Mô tả:** Implement structured logging, metrics tracking, và health checks

## Key Insights
- Structured logging với ILogger
- Track key metrics: receipt rate, processing time, queue depth, error rate
- Health checks cho queue, database, external APIs
- Application Insights hoặc OpenTelemetry cho production

## Requirements

**Functional:**
- Structured logging cho tất cả operations
- Track webhook receipt rate
- Track processing latency (P50, P95, P99)
- Track queue depth
- Track API error rates
- Health check endpoints

**Non-Functional:**
- Low overhead (< 5ms per log)
- Queryable logs
- Real-time metrics
- Alerting capability

## Architecture

**Logging Layers:**
```
Application → ILogger → Console/File/AppInsights
            ↓
    Structured logs (JSON)
            ↓
    Metrics aggregation
            ↓
    Health checks
```

**Key Metrics:**
- `webhook.received.count` - Total webhooks received
- `webhook.processing.duration` - Processing time histogram
- `webhook.queue.depth` - Current queue depth
- `webhook.errors.count` - Error count by type
- `graph_api.calls.count` - API call count
- `graph_api.errors.count` - API error count

## Related Code Files

**To Create:**
- `src/MessengerWebhook/Middleware/MetricsMiddleware.cs`
- `src/MessengerWebhook/Services/MetricsService.cs`

**To Modify:**
- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/appsettings.json`

## Implementation Steps

1. **Configure structured logging**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "MessengerWebhook": "Debug"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "SingleLine": true,
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff ",
        "UseUtcTimestamp": true,
        "JsonWriterOptions": {
          "Indented": false
        }
      }
    }
  }
}
```

2. **Implement MetricsService**
```csharp
public class MetricsService
{
    private long _webhooksReceived;
    private long _webhooksProcessed;
    private long _webhookErrors;
    private long _graphApiCalls;
    private long _graphApiErrors;
    private readonly ConcurrentBag<long> _processingTimes = new();

    public void RecordWebhookReceived() => Interlocked.Increment(ref _webhooksReceived);
    public void RecordWebhookProcessed(long durationMs)
    {
        Interlocked.Increment(ref _webhooksProcessed);
        _processingTimes.Add(durationMs);
    }
    public void RecordWebhookError() => Interlocked.Increment(ref _webhookErrors);
    public void RecordGraphApiCall() => Interlocked.Increment(ref _graphApiCalls);
    public void RecordGraphApiError() => Interlocked.Increment(ref _graphApiErrors);

    public MetricsSnapshot GetSnapshot()
    {
        var times = _processingTimes.ToArray();
        Array.Sort(times);

        return new MetricsSnapshot
        {
            WebhooksReceived = _webhooksReceived,
            WebhooksProcessed = _webhooksProcessed,
            WebhookErrors = _webhookErrors,
            GraphApiCalls = _graphApiCalls,
            GraphApiErrors = _graphApiErrors,
            ProcessingTimeP50 = GetPercentile(times, 0.5),
            ProcessingTimeP95 = GetPercentile(times, 0.95),
            ProcessingTimeP99 = GetPercentile(times, 0.99)
        };
    }

    private static long GetPercentile(long[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        var index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}
```

3. **Add metrics endpoint**
```csharp
app.MapGet("/metrics", (MetricsService metrics, Channel<MessagingEvent> channel) =>
{
    var snapshot = metrics.GetSnapshot();
    return Results.Ok(new
    {
        snapshot.WebhooksReceived,
        snapshot.WebhooksProcessed,
        snapshot.WebhookErrors,
        snapshot.GraphApiCalls,
        snapshot.GraphApiErrors,
        QueueDepth = channel.Reader.Count,
        ProcessingLatency = new
        {
            P50 = snapshot.ProcessingTimeP50,
            P95 = snapshot.ProcessingTimeP95,
            P99 = snapshot.ProcessingTimeP99
        }
    });
});
```

4. **Implement health checks**
```csharp
builder.Services.AddHealthChecks()
    .AddCheck("webhook_queue", () =>
    {
        var depth = channel.Reader.Count;
        return depth < 1000
            ? HealthCheckResult.Healthy($"Queue depth: {depth}")
            : HealthCheckResult.Degraded($"Queue depth high: {depth}");
    })
    .AddCheck("graph_api", async () =>
    {
        try
        {
            var response = await httpClient.GetAsync("https://graph.facebook.com/v21.0/me?access_token=...");
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Graph API unreachable");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Graph API error");
        }
    });

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});
```

5. **Add logging vào các services**
```csharp
// WebhookProcessor
_logger.LogInformation(
    "Processing message: SenderId={SenderId}, MessageId={MessageId}, Text={Text}",
    evt.Sender.Id,
    evt.Message?.Mid,
    evt.Message?.Text);

// MessengerService
_logger.LogInformation(
    "Sending message: RecipientId={RecipientId}, TextLength={Length}",
    recipientId,
    text.Length);

_logger.LogError(
    "Graph API error: StatusCode={StatusCode}, Error={Error}",
    response.StatusCode,
    error);
```

6. **Write tests**
- MetricsService_RecordWebhook_IncrementsCounter
- MetricsService_GetSnapshot_ReturnsCorrectPercentiles
- HealthCheck_QueueDepthNormal_ReturnsHealthy
- HealthCheck_QueueDepthHigh_ReturnsDegraded

## Todo List
- [ ] Configure structured logging trong appsettings.json
- [ ] Implement MetricsService
- [ ] Add metrics endpoint /metrics
- [ ] Implement health checks
- [ ] Add logging vào WebhookProcessor
- [ ] Add logging vào MessengerService
- [ ] Write unit tests
- [ ] Test metrics endpoint
- [ ] Test health checks endpoint

## Success Criteria
- Structured logs output JSON format
- Metrics endpoint trả về accurate data
- Health checks reflect system state
- P95 processing latency tracked
- Queue depth monitored
- All tests pass

## Risk Assessment
- **Risk:** Metrics overhead
  - **Mitigation:** Use lightweight counters, sample percentiles
- **Risk:** Log volume too high
  - **Mitigation:** Adjust log levels per environment

## Security Considerations
- Don't log sensitive data (tokens, user messages)
- Sanitize PII before logging
- Secure metrics endpoint (add auth sau)

## Next Steps
- Phase 8: Testing
