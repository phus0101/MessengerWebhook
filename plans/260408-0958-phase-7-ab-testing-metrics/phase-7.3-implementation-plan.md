---
title: "Phase 7.3 Implementation Plan: Metrics API & Reporting"
description: "Build API endpoints for A/B test metrics aggregation and analysis"
status: pending
priority: P2
effort: 3h
branch: master
tags: [metrics, api, ab-testing, reporting]
created: 2026-04-08
---

# Phase 7.3 Implementation Plan: Metrics API & Reporting

## Context

Build API endpoints to query and aggregate metrics data collected by Phase 7.2. Provides data access for A/B test analysis, dashboards, and external analytics tools.

**Dependencies:**
- Phase 7.2 complete: `ConversationMetric` entity and collection service operational
- Metrics data available in `conversation_metrics` table
- Admin authentication system in place

**Key Constraints:**
- Query latency <500ms for 10K metrics
- Pagination: 100 items per page
- 5min response caching
- Admin-only endpoints
- Tenant isolation via query filters

## Architecture Overview

### Data Flow

```
HTTP Request → Authorization Check → Tenant Filter
    ↓
MetricsController → MetricsAggregationService
    ↓
Query conversation_metrics table (filtered by TenantId + date range)
    ↓
In-memory aggregation (LINQ GroupBy, Average, Count)
    ↓
Response models (MetricsSummary, VariantComparison, PipelinePerformance)
    ↓
Response caching (5min TTL) → JSON response
```

### Components

**1. Response Models** (`Services/Metrics/Models/`)
- `MetricsSummary.cs` - Overall A/B test summary
- `VariantComparison.cs` - Control vs treatment comparison
- `PipelinePerformance.cs` - Pipeline latency breakdown

**2. Aggregation Service** (`Services/Metrics/`)
- `IMetricsAggregationService.cs` - Interface
- `MetricsAggregationService.cs` - Implementation with LINQ aggregations

**3. API Endpoints** (`Endpoints/`)
- `MetricsEndpointExtensions.cs` - Minimal API endpoints
- Pattern: `/admin/api/metrics/*` (follows existing admin endpoint convention)

**4. Caching Strategy**
- Use `IDistributedCache` (already configured in Program.cs)
- Cache key pattern: `metrics:{endpoint}:{tenantId}:{startDate}:{endDate}`
- TTL: 5 minutes (acceptable staleness for analytics)

## Implementation Steps

### Step 1: Create Response Models (30min)

**Files to create:**
- `src/MessengerWebhook/Services/Metrics/Models/MetricsSummary.cs`
- `src/MessengerWebhook/Services/Metrics/Models/VariantComparison.cs`
- `src/MessengerWebhook/Services/Metrics/Models/PipelinePerformance.cs`

**MetricsSummary.cs:**
```csharp
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
```

**VariantComparison.cs:**
```csharp
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
```

**PipelinePerformance.cs:**
```csharp
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
```

### Step 2: Implement Aggregation Service (60min)

**Files to create:**
- `src/MessengerWebhook/Services/Metrics/IMetricsAggregationService.cs`
- `src/MessengerWebhook/Services/Metrics/MetricsAggregationService.cs`

**IMetricsAggregationService.cs:**
```csharp
using MessengerWebhook.Services.Metrics.Models;

namespace MessengerWebhook.Services.Metrics;

public interface IMetricsAggregationService
{
    Task<MetricsSummary> GetSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<VariantComparison> GetVariantComparisonAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<PipelinePerformance> GetPipelinePerformanceAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
```

**MetricsAggregationService.cs:**
```csharp
using MessengerWebhook.Data;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.Tenants;
using Microsoft.EntityFrameworkCore;

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

    public async Task<MetricsSummary> GetSummaryAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = tenantId ?? _tenantContext.TenantId;

        var metrics = await _dbContext.ConversationMetrics
            .Where(m => m.TenantId == effectiveTenantId
                && m.MessageTimestamp >= startDate
                && m.MessageTimestamp <= endDate)
            .ToListAsync(cancellationToken);

        var controlMetrics = metrics.Where(m => m.ABTestVariant == "control").ToList();
        var treatmentMetrics = metrics.Where(m => m.ABTestVariant == "treatment").ToList();

        var controlSessions = controlMetrics.Select(m => m.SessionId).Distinct().Count();
        var treatmentSessions = treatmentMetrics.Select(m => m.SessionId).Distinct().Count();

        return new MetricsSummary
        {
            Period = new DateRange(startDate, endDate),
            TotalSessions = controlSessions + treatmentSessions,
            TotalMessages = metrics.Count,
            Variants = new VariantStats
            {
                Control = new VariantCount(controlSessions, controlMetrics.Count),
                Treatment = new VariantCount(treatmentSessions, treatmentMetrics.Count)
            },
            AvgResponseTimeMs = new ResponseTimeStats(
                controlMetrics.Any() ? (int)controlMetrics.Average(m => m.TotalResponseTimeMs) : 0,
                treatmentMetrics.Any() ? (int)treatmentMetrics.Average(m => m.TotalResponseTimeMs) : 0
            )
        };
    }

    public async Task<VariantComparison> GetVariantComparisonAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = tenantId ?? _tenantContext.TenantId;

        var metrics = await _dbContext.ConversationMetrics
            .Where(m => m.TenantId == effectiveTenantId
                && m.MessageTimestamp >= startDate
                && m.MessageTimestamp <= endDate)
            .ToListAsync(cancellationToken);

        var controlMetrics = metrics.Where(m => m.ABTestVariant == "control").ToList();
        var treatmentMetrics = metrics.Where(m => m.ABTestVariant == "treatment").ToList();

        return new VariantComparison
        {
            Control = CalculateVariantMetrics(controlMetrics, isControl: true),
            Treatment = CalculateVariantMetrics(treatmentMetrics, isControl: false)
        };
    }

    public async Task<PipelinePerformance> GetPipelinePerformanceAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = tenantId ?? _tenantContext.TenantId;

        var treatmentMetrics = await _dbContext.ConversationMetrics
            .Where(m => m.TenantId == effectiveTenantId
                && m.ABTestVariant == "treatment"
                && m.MessageTimestamp >= startDate
                && m.MessageTimestamp <= endDate
                && m.PipelineLatencyMs.HasValue)
            .ToListAsync(cancellationToken);

        if (!treatmentMetrics.Any())
        {
            return new PipelinePerformance
            {
                AvgLatencyMs = new LatencyBreakdown { Total = 0 },
                P95LatencyMs = new PercentileLatency(0)
            };
        }

        // Calculate P95 (95th percentile)
        var sortedLatencies = treatmentMetrics
            .Select(m => m.PipelineLatencyMs!.Value)
            .OrderBy(x => x)
            .ToList();
        var p95Index = (int)Math.Ceiling(sortedLatencies.Count * 0.95) - 1;
        var p95Latency = sortedLatencies[Math.Max(0, p95Index)];

        return new PipelinePerformance
        {
            AvgLatencyMs = new LatencyBreakdown
            {
                // Note: Individual component latencies not tracked in Phase 7.2
                // These would require additional instrumentation
                EmotionDetection = null,
                ToneMatching = null,
                ContextAnalysis = null,
                SmallTalkDetection = null,
                ResponseValidation = null,
                Total = (int)treatmentMetrics.Average(m => m.PipelineLatencyMs!.Value)
            },
            P95LatencyMs = new PercentileLatency(p95Latency)
        };
    }

    private VariantMetrics CalculateVariantMetrics(
        List<Data.Entities.ConversationMetric> metrics,
        bool isControl)
    {
        if (!metrics.Any())
        {
            return new VariantMetrics
            {
                Sessions = 0,
                AvgMessagesPerSession = 0,
                CompletionRate = 0,
                EscalationRate = 0,
                AbandonmentRate = 0,
                AvgResponseTimeMs = 0
            };
        }

        var sessionGroups = metrics.GroupBy(m => m.SessionId).ToList();
        var sessions = sessionGroups.Count;

        var completedSessions = sessionGroups.Count(g =>
            g.Any(m => m.ConversationOutcome == "completed"));
        var escalatedSessions = sessionGroups.Count(g =>
            g.Any(m => m.ConversationOutcome == "escalated"));
        var abandonedSessions = sessionGroups.Count(g =>
            g.Any(m => m.ConversationOutcome == "abandoned"));

        var result = new VariantMetrics
        {
            Sessions = sessions,
            AvgMessagesPerSession = (decimal)metrics.Count / sessions,
            CompletionRate = (decimal)completedSessions / sessions,
            EscalationRate = (decimal)escalatedSessions / sessions,
            AbandonmentRate = (decimal)abandonedSessions / sessions,
            AvgResponseTimeMs = (int)metrics.Average(m => m.TotalResponseTimeMs)
        };

        // Treatment-only metrics
        if (!isControl)
        {
            var metricsWithEmotion = metrics.Where(m => m.DetectedEmotion != null).ToList();
            var metricsWithTone = metrics.Where(m => m.MatchedTone != null).ToList();
            var metricsWithValidation = metrics.Where(m => m.ValidationPassed.HasValue).ToList();

            return result with
            {
                EmotionAccuracy = metricsWithEmotion.Any()
                    ? (decimal)metricsWithEmotion.Count(m => m.EmotionConfidence >= 0.7m) / metricsWithEmotion.Count
                    : null,
                ToneMatchingRate = metricsWithTone.Any()
                    ? (decimal)metricsWithTone.Count / metrics.Count
                    : null,
                ValidationPassRate = metricsWithValidation.Any()
                    ? (decimal)metricsWithValidation.Count(m => m.ValidationPassed == true) / metricsWithValidation.Count
                    : null
            };
        }

        return result;
    }
}
```

### Step 3: Create API Endpoints (45min)

**File to create:**
- `src/MessengerWebhook/Endpoints/MetricsEndpointExtensions.cs`

**MetricsEndpointExtensions.cs:**
```csharp
using MessengerWebhook.Services.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MessengerWebhook.Endpoints;

public static class MetricsEndpointExtensions
{
    public static RouteGroupBuilder MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/api/metrics").RequireAuthorization();

        group.MapGet("/summary", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:summary:{user.TenantId}:{start:yyyyMMdd}:{end:yyyyMMdd}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<object>(cached);
                return Results.Ok(cachedResult);
            }

            var summary = await metricsService.GetSummaryAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(summary),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(summary);
        })
        .WithName("GetMetricsSummary")
        .WithOpenApi();

        group.MapGet("/variants", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:variants:{user.TenantId}:{start:yyyyMMdd}:{end:yyyyMMdd}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<object>(cached);
                return Results.Ok(cachedResult);
            }

            var comparison = await metricsService.GetVariantComparisonAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(comparison),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(comparison);
        })
        .WithName("GetVariantComparison")
        .WithOpenApi();

        group.MapGet("/pipeline", async (
            DateTime? startDate,
            DateTime? endDate,
            HttpContext httpContext,
            IMetricsAggregationService metricsService,
            IDistributedCache cache,
            CancellationToken cancellationToken) =>
        {
            var user = AdminApiEndpointHelpers.GetUser(httpContext);
            if (user == null) return Results.Unauthorized();

            var start = startDate ?? DateTime.UtcNow.AddDays(-14);
            var end = endDate ?? DateTime.UtcNow;

            var cacheKey = $"metrics:pipeline:{user.TenantId}:{start:yyyyMMdd}:{end:yyyyMMdd}";
            var cached = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (cached != null)
            {
                var cachedResult = JsonSerializer.Deserialize<object>(cached);
                return Results.Ok(cachedResult);
            }

            var performance = await metricsService.GetPipelinePerformanceAsync(start, end, user.TenantId, cancellationToken);

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(performance),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
                cancellationToken);

            return Results.Ok(performance);
        })
        .WithName("GetPipelinePerformance")
        .WithOpenApi();

        return group;
    }
}
```

### Step 4: Register Services and Endpoints (20min)

**File to modify:**
- `src/MessengerWebhook/Program.cs`

**Changes:**

1. Add service registration (after line ~200, near other service registrations):
```csharp
// Metrics aggregation service
builder.Services.AddScoped<IMetricsAggregationService, MetricsAggregationService>();
```

2. Add endpoint mapping (after line ~629, near other endpoint mappings):
```csharp
app.MapMetricsEndpoints();
```

3. Add using statement at top:
```csharp
using MessengerWebhook.Services.Metrics;
```

### Step 5: Compile and Verify (15min)

**Commands:**
```bash
# Build solution
dotnet build

# Check for compilation errors
# Fix any missing usings or type mismatches

# Verify endpoints registered
dotnet run --project src/MessengerWebhook
# Check logs for "Mapped /admin/api/metrics/*"
```

**Expected output:**
- No compilation errors
- All endpoints registered successfully
- Application starts without errors

### Step 6: Manual Testing (30min)

**Prerequisites:**
- Admin user authenticated
- Metrics data exists in database (from Phase 7.2)

**Test scenarios:**

1. **Test /admin/api/metrics/summary**
```bash
curl -X GET "http://localhost:5030/admin/api/metrics/summary?startDate=2026-04-01&endDate=2026-04-08" \
  -H "Cookie: .AspNetCore.Cookies=<auth-cookie>"
```

Expected response:
```json
{
  "period": { "start": "2026-04-01T00:00:00Z", "end": "2026-04-08T00:00:00Z" },
  "totalSessions": 125,
  "totalMessages": 842,
  "variants": {
    "control": { "sessions": 63, "messages": 421 },
    "treatment": { "sessions": 62, "messages": 421 }
  },
  "avgResponseTimeMs": { "control": 850, "treatment": 920 }
}
```

2. **Test /admin/api/metrics/variants**
```bash
curl -X GET "http://localhost:5030/admin/api/metrics/variants?startDate=2026-04-01&endDate=2026-04-08" \
  -H "Cookie: .AspNetCore.Cookies=<auth-cookie>"
```

Expected response:
```json
{
  "control": {
    "sessions": 63,
    "avgMessagesPerSession": 6.7,
    "completionRate": 0.68,
    "escalationRate": 0.12,
    "abandonmentRate": 0.20,
    "avgResponseTimeMs": 850,
    "emotionAccuracy": null,
    "toneMatchingRate": null,
    "validationPassRate": null
  },
  "treatment": {
    "sessions": 62,
    "avgMessagesPerSession": 6.8,
    "completionRate": 0.75,
    "escalationRate": 0.08,
    "abandonmentRate": 0.17,
    "avgResponseTimeMs": 920,
    "emotionAccuracy": 0.87,
    "toneMatchingRate": 0.92,
    "validationPassRate": 0.94
  }
}
```

3. **Test /admin/api/metrics/pipeline**
```bash
curl -X GET "http://localhost:5030/admin/api/metrics/pipeline?startDate=2026-04-01&endDate=2026-04-08" \
  -H "Cookie: .AspNetCore.Cookies=<auth-cookie>"
```

Expected response:
```json
{
  "avgLatencyMs": {
    "emotionDetection": null,
    "toneMatching": null,
    "contextAnalysis": null,
    "smallTalkDetection": null,
    "responseValidation": null,
    "total": 92
  },
  "p95LatencyMs": { "total": 98 }
}
```

4. **Test caching**
- Make same request twice
- Second request should be faster (<50ms)
- Check logs for cache hit

5. **Test authorization**
```bash
# Without auth cookie - should return 401
curl -X GET "http://localhost:5030/admin/api/metrics/summary"
```

6. **Test tenant isolation**
- Login as different tenant admin
- Verify only that tenant's metrics returned

7. **Test date range filtering**
```bash
# Last 7 days
curl -X GET "http://localhost:5030/admin/api/metrics/summary?startDate=2026-04-01&endDate=2026-04-08"

# Last 30 days
curl -X GET "http://localhost:5030/admin/api/metrics/summary?startDate=2026-03-09&endDate=2026-04-08"
```

8. **Test query performance**
- Seed 10K metrics (use Phase 7.2 service)
- Measure response time (should be <500ms)

## Success Criteria

**Technical:**
- [ ] All endpoints return valid JSON
- [ ] Query latency <500ms for 10K metrics
- [ ] Tenant isolation enforced (global query filters)
- [ ] Caching working (5min TTL, cache hits logged)
- [ ] Authorization enforced (401 without auth)
- [ ] No compilation errors
- [ ] No runtime exceptions

**Business:**
- [ ] A/B test comparison data accessible
- [ ] Control vs treatment metrics clearly differentiated
- [ ] Pipeline performance metrics visible
- [ ] Data sufficient for statistical analysis
- [ ] Date range filtering works correctly

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Query performance degradation with large datasets | Medium | Medium | Add indexes on `TenantId`, `MessageTimestamp`, `ABTestVariant`; implement pagination if needed |
| Unauthorized data access | Low | High | Admin authorization required; tenant filtering enforced |
| Cache invalidation issues | Low | Low | Short TTL (5min); acceptable staleness for analytics |
| Division by zero in aggregations | Low | Medium | Check for empty collections before calculating averages |
| Memory pressure from large result sets | Low | Medium | Use streaming queries; limit date ranges |

## Security Considerations

- **Authorization:** Admin-only endpoints via `.RequireAuthorization()`
- **Tenant Isolation:** All queries filtered by `TenantId` from `ITenantContext`
- **No PII:** Metrics contain PSID (Facebook identifier), not user real identity
- **Rate Limiting:** Consider adding rate limiting on metrics endpoints (defer to Phase 8)
- **Input Validation:** Date ranges validated (start < end, max 90 days)

## Performance Optimizations

**Database Indexes (recommended):**
```sql
CREATE INDEX idx_conversation_metrics_tenant_date 
ON conversation_metrics(tenant_id, message_timestamp);

CREATE INDEX idx_conversation_metrics_variant 
ON conversation_metrics(ab_test_variant);

CREATE INDEX idx_conversation_metrics_session 
ON conversation_metrics(session_id);
```

**Query Optimization:**
- Use `ToListAsync()` to load data once, then aggregate in-memory
- Avoid N+1 queries by loading all metrics in single query
- Use LINQ for aggregations (faster than multiple DB queries)

**Caching Strategy:**
- Cache key includes tenant, date range for isolation
- 5min TTL balances freshness vs performance
- Use `IDistributedCache` for multi-instance deployments

## Testing Strategy

**Manual Testing (this phase):**
- Endpoint functionality
- Authorization checks
- Tenant isolation
- Date range filtering
- Cache behavior
- Query performance

**Automated Testing (Phase 7.4):**
- Unit tests for `MetricsAggregationService`
- Integration tests for endpoints
- Performance tests with 10K metrics
- Security tests (unauthorized access)

## Next Steps

After Phase 7.3 completion:
1. Verify all endpoints working with real data
2. Check query performance with production-like dataset
3. Proceed to Phase 7.4: Testing & Validation
4. Consider dashboard implementation (post-MVP)

## Unresolved Questions

1. **Dashboard UI:** Build custom React dashboard or defer? 
   - **Recommendation:** Defer. API-first approach allows flexibility (custom dashboard, Grafana, external tools)

2. **Export functionality:** Add CSV/Excel export?
   - **Recommendation:** Defer to Phase 8. API provides data; export can be added later

3. **Real-time metrics:** WebSocket updates or polling?
   - **Recommendation:** Polling sufficient for MVP. 5min cache means near-real-time

4. **Pagination:** Implement pagination for large result sets?
   - **Recommendation:** Not needed for aggregated results (always small). Add if raw metrics endpoint needed

5. **Component-level latency:** Track individual pipeline component latencies?
   - **Recommendation:** Defer. Requires additional instrumentation in Phase 7.2. Total latency sufficient for MVP

## File Checklist

**Files to Create:**
- [ ] `Services/Metrics/Models/MetricsSummary.cs`
- [ ] `Services/Metrics/Models/VariantComparison.cs`
- [ ] `Services/Metrics/Models/PipelinePerformance.cs`
- [ ] `Services/Metrics/IMetricsAggregationService.cs`
- [ ] `Services/Metrics/MetricsAggregationService.cs`
- [ ] `Endpoints/MetricsEndpointExtensions.cs`

**Files to Modify:**
- [ ] `Program.cs` - Register service and map endpoints

**Total Files:** 6 new, 1 modified
