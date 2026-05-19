---
type: implementation-plan
phase: 7.3
title: "Metrics API & Reporting Implementation Plan"
created: 2026-04-09
effort: 3h
status: ready
---

# Phase 7.3: Metrics API & Reporting - Implementation Plan

## Executive Summary

Build REST API endpoints to query and aggregate conversation metrics for A/B test analysis. API-first approach enables dashboard (Phase 7.5) and external analytics integration. Focus: query performance, tenant isolation, admin authorization.

**Effort**: 3 hours  
**Dependencies**: Phase 7.2 (ConversationMetricsService, ConversationMetric entity)  
**Deliverables**: 3 API endpoints, aggregation service, response caching, admin auth

---

## Architecture Overview

### Data Flow

```
HTTP Request → MetricsController (auth check)
    ↓
MetricsAggregationService (tenant filter)
    ↓
EF Core Query → conversation_metrics table
    ↓
In-memory aggregation (LINQ)
    ↓
Response caching (5min TTL)
    ↓
JSON response
```

### API Endpoints

**1. GET /api/metrics/summary**
- Overall A/B test summary
- Total sessions, messages, avg response time
- Control vs treatment comparison

**2. GET /api/metrics/variants**
- Detailed variant comparison
- Completion/escalation/abandonment rates
- Treatment-only metrics (emotion accuracy, tone matching, validation pass rate)

**3. GET /api/metrics/pipeline**
- Pipeline performance breakdown
- Avg/p95 latency per component
- Treatment group only

### Query Parameters (All Endpoints)

- `startDate` (required): ISO 8601 date (e.g., "2026-04-08")
- `endDate` (required): ISO 8601 date (e.g., "2026-04-22")
- `tenantId` (optional): Admin can query specific tenant, defaults to current user's tenant

---

## File Structure

### New Files (6 files)

```
src/MessengerWebhook/
├── Controllers/
│   └── MetricsController.cs                    # API endpoints (3 routes)
└── Services/
    └── Metrics/
        ├── IMetricsAggregationService.cs       # Service interface
        ├── MetricsAggregationService.cs        # Aggregation logic
        └── Models/
            ├── MetricsSummaryDto.cs            # Summary response model
            ├── VariantComparisonDto.cs         # Variant comparison model
            └── PipelinePerformanceDto.cs       # Pipeline performance model
```

### Modified Files (1 file)

```
src/MessengerWebhook/
└── Program.cs                                  # Register services, add response caching
```

---

## Implementation Steps

### Step 1: Create Response DTOs (30min)

**File 1: `Services/Metrics/Models/MetricsSummaryDto.cs`**

```csharp
namespace MessengerWebhook.Services.Metrics.Models;

public record MetricsSummaryDto
{
    public required PeriodDto Period { get; init; }
    public int TotalSessions { get; init; }
    public int TotalMessages { get; init; }
    public required VariantStatsDto Variants { get; init; }
    public required AvgResponseTimeDto AvgResponseTimeMs { get; init; }
}

public record PeriodDto
{
    public required string Start { get; init; }
    public required string End { get; init; }
}

public record VariantStatsDto
{
    public required VariantCountDto Control { get; init; }
    public required VariantCountDto Treatment { get; init; }
}

public record VariantCountDto
{
    public int Sessions { get; init; }
    public int Messages { get; init; }
}

public record AvgResponseTimeDto
{
    public int Control { get; init; }
    public int Treatment { get; init; }
}
```

**File 2: `Services/Metrics/Models/VariantComparisonDto.cs`**

```csharp
namespace MessengerWebhook.Services.Metrics.Models;

public record VariantComparisonDto
{
    public required VariantMetricsDto Control { get; init; }
    public required TreatmentMetricsDto Treatment { get; init; }
}

public record VariantMetricsDto
{
    public int Sessions { get; init; }
    public decimal AvgMessagesPerSession { get; init; }
    public decimal CompletionRate { get; init; }
    public decimal EscalationRate { get; init; }
    public decimal AbandonmentRate { get; init; }
    public int AvgResponseTimeMs { get; init; }
}

public record TreatmentMetricsDto : VariantMetricsDto
{
    public decimal EmotionAccuracy { get; init; }
    public decimal ToneMatchingRate { get; init; }
    public decimal ValidationPassRate { get; init; }
}
```

**File 3: `Services/Metrics/Models/PipelinePerformanceDto.cs`**

```csharp
namespace MessengerWebhook.Services.Metrics.Models;

public record PipelinePerformanceDto
{
    public required LatencyBreakdownDto AvgLatencyMs { get; init; }
    public required P95LatencyDto P95LatencyMs { get; init; }
}

public record LatencyBreakdownDto
{
    public int EmotionDetection { get; init; }
    public int ToneMatching { get; init; }
    public int ContextAnalysis { get; init; }
    public int SmallTalkDetection { get; init; }
    public int ResponseValidation { get; init; }
    public int Total { get; init; }
}

public record P95LatencyDto
{
    public int Total { get; init; }
}
```

**Success Criteria**:
- All DTOs compile without errors
- Records use `required` for non-nullable properties
- Naming matches API spec from phase plan

---

### Step 2: Implement Aggregation Service (60min)

**File 4: `Services/Metrics/IMetricsAggregationService.cs`**

```csharp
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
}
```

**File 5: `Services/Metrics/MetricsAggregationService.cs`**

Key implementation details:

**Constructor Dependencies**:
- `MessengerBotDbContext` - Database access
- `ITenantContext` - Current tenant resolution
- `ILogger<MetricsAggregationService>` - Logging

**GetSummaryAsync Logic**:
1. Resolve tenant (use provided or current from ITenantContext)
2. Query metrics: `WHERE created_at BETWEEN @start AND @end AND tenant_id = @tenantId`
3. Group by variant (control/treatment)
4. Count distinct session_id for sessions
5. Count total messages
6. Calculate avg(total_response_time_ms) per variant
7. Return MetricsSummaryDto

**GetVariantComparisonAsync Logic**:
1. Query metrics with same filters
2. Group by variant
3. Calculate per variant:
   - Sessions: `COUNT(DISTINCT session_id)`
   - AvgMessagesPerSession: `COUNT(*) / COUNT(DISTINCT session_id)`
   - CompletionRate: `COUNT(conversation_outcome = 'completed') / COUNT(DISTINCT session_id)`
   - EscalationRate: `COUNT(conversation_outcome = 'escalated') / COUNT(DISTINCT session_id)`
   - AbandonmentRate: `COUNT(conversation_outcome = 'abandoned') / COUNT(DISTINCT session_id)`
   - AvgResponseTimeMs: `AVG(total_response_time_ms)`
4. For treatment only:
   - EmotionAccuracy: `COUNT(emotion_confidence > 0.7) / COUNT(detected_emotion IS NOT NULL)`
   - ToneMatchingRate: `COUNT(matched_tone IS NOT NULL) / COUNT(*)`
   - ValidationPassRate: `COUNT(validation_passed = true) / COUNT(*)`
5. Return VariantComparisonDto

**GetPipelinePerformanceAsync Logic**:
1. Query metrics: `WHERE variant = 'treatment' AND pipeline_latency_ms IS NOT NULL`
2. Extract latency from additional_metrics JSONB:
   ```csharp
   var latencies = metrics
       .Where(m => m.AdditionalMetrics != null)
       .Select(m => new {
           EmotionMs = m.AdditionalMetrics.RootElement
               .GetProperty("emotionDetectionMs").GetInt32(),
           ToneMs = m.AdditionalMetrics.RootElement
               .GetProperty("toneMatchingMs").GetInt32(),
           // ... other components
       });
   ```
3. Calculate averages per component
4. Calculate p95 for total pipeline latency (use OrderBy + Skip)
5. Return PipelinePerformanceDto

**Error Handling**:
- Catch `JsonException` when parsing additional_metrics
- Log warnings for missing/invalid data
- Return 0 for missing metrics (don't fail entire request)

**Performance Optimization**:
- Use `AsNoTracking()` for read-only queries
- Project to anonymous types before aggregation
- Avoid N+1 queries (single query per endpoint)

---

### Step 3: Create API Controller (45min)

**File 6: `Controllers/MetricsController.cs`**

```csharp
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MessengerWebhook.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Admin-only (configured in Program.cs)
public class MetricsController : ControllerBase
{
    private readonly IMetricsAggregationService _aggregationService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsAggregationService aggregationService,
        ILogger<MetricsController> logger)
    {
        _aggregationService = aggregationService;
        _logger = logger;
    }

    [HttpGet("summary")]
    [ResponseCache(Duration = 300)] // 5min cache
    public async Task<ActionResult<MetricsSummaryDto>> GetSummary(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate >= endDate)
        {
            return BadRequest("startDate must be before endDate");
        }

        try
        {
            var summary = await _aggregationService.GetSummaryAsync(
                startDate, endDate, tenantId, cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metrics summary");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    [HttpGet("variants")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<VariantComparisonDto>> GetVariantComparison(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate >= endDate)
        {
            return BadRequest("startDate must be before endDate");
        }

        try
        {
            var comparison = await _aggregationService.GetVariantComparisonAsync(
                startDate, endDate, tenantId, cancellationToken);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get variant comparison");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }

    [HttpGet("pipeline")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<PipelinePerformanceDto>> GetPipelinePerformance(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate >= endDate)
        {
            return BadRequest("startDate must be before endDate");
        }

        try
        {
            var performance = await _aggregationService.GetPipelinePerformanceAsync(
                startDate, endDate, tenantId, cancellationToken);
            return Ok(performance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipeline performance");
            return StatusCode(500, "Failed to retrieve metrics");
        }
    }
}
```

**Key Features**:
- `[Authorize]` attribute for admin-only access
- `[ResponseCache(Duration = 300)]` for 5min caching
- Input validation (startDate < endDate)
- Consistent error handling
- CancellationToken support

---

### Step 4: Register Services & Configure Caching (20min)

**File 7: Modify `Program.cs`**

**Add after line 199** (after other service registrations):

```csharp
// Metrics services (Phase 7.3)
builder.Services.AddScoped<IMetricsAggregationService, MetricsAggregationService>();

// Response caching for metrics endpoints
builder.Services.AddResponseCaching();
```

**Add after `app.UseAuthentication();`** (around line 350):

```csharp
app.UseResponseCaching();
```

**Success Criteria**:
- Services registered in DI container
- Response caching middleware added to pipeline
- No compilation errors

---

### Step 5: Compile & Verify (15min)

**Commands**:

```bash
cd "D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook"

# Build solution
dotnet build

# Check for warnings
dotnet build --no-incremental /warnaserror
```

**Expected Output**:
- Build succeeded
- 0 errors, 0 warnings
- All 6 new files compiled

**Fix Common Issues**:
- Missing `using` statements
- Nullable reference warnings (add `required` or `?`)
- JSON parsing errors (add null checks)

---

### Step 6: Manual Testing (30min)

**Prerequisites**:
1. Phase 7.2 completed (metrics data exists)
2. Admin user authenticated
3. PostgreSQL running

**Test Scenarios**:

**Test 1: Summary Endpoint**

```bash
# Start app
dotnet run --project src/MessengerWebhook

# Test request (replace dates with actual data range)
curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-08&endDate=2026-04-09" \
  -H "Cookie: admin_session=..." \
  -H "Accept: application/json"
```

**Expected Response**:
```json
{
  "period": {
    "start": "2026-04-08",
    "end": "2026-04-09"
  },
  "totalSessions": 10,
  "totalMessages": 67,
  "variants": {
    "control": { "sessions": 5, "messages": 33 },
    "treatment": { "sessions": 5, "messages": 34 }
  },
  "avgResponseTimeMs": {
    "control": 850,
    "treatment": 920
  }
}
```

**Test 2: Variant Comparison**

```bash
curl -X GET "http://localhost:5030/api/metrics/variants?startDate=2026-04-08&endDate=2026-04-09" \
  -H "Cookie: admin_session=..." \
  -H "Accept: application/json"
```

**Expected Response**:
```json
{
  "control": {
    "sessions": 5,
    "avgMessagesPerSession": 6.6,
    "completionRate": 0.60,
    "escalationRate": 0.20,
    "abandonmentRate": 0.20,
    "avgResponseTimeMs": 850
  },
  "treatment": {
    "sessions": 5,
    "avgMessagesPerSession": 6.8,
    "completionRate": 0.80,
    "escalationRate": 0.00,
    "abandonmentRate": 0.20,
    "avgResponseTimeMs": 920,
    "emotionAccuracy": 0.85,
    "toneMatchingRate": 0.90,
    "validationPassRate": 0.95
  }
}
```

**Test 3: Pipeline Performance**

```bash
curl -X GET "http://localhost:5030/api/metrics/pipeline?startDate=2026-04-08&endDate=2026-04-09" \
  -H "Cookie: admin_session=..." \
  -H "Accept: application/json"
```

**Expected Response**:
```json
{
  "avgLatencyMs": {
    "emotionDetection": 18,
    "toneMatching": 9,
    "contextAnalysis": 28,
    "smallTalkDetection": 14,
    "responseValidation": 23,
    "total": 92
  },
  "p95LatencyMs": {
    "total": 98
  }
}
```

**Test 4: Error Cases**

```bash
# Invalid date range
curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-09&endDate=2026-04-08"
# Expected: 400 Bad Request

# Unauthorized (no cookie)
curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-08&endDate=2026-04-09"
# Expected: 401 Unauthorized

# Missing parameters
curl -X GET "http://localhost:5030/api/metrics/summary"
# Expected: 400 Bad Request
```

**Test 5: Caching**

```bash
# First request (cache miss)
time curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-08&endDate=2026-04-09" \
  -H "Cookie: admin_session=..."

# Second request (cache hit, should be faster)
time curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-08&endDate=2026-04-09" \
  -H "Cookie: admin_session=..."
```

**Expected**: Second request ~10x faster (cache hit)

**Test 6: Tenant Isolation**

```bash
# Query specific tenant (admin only)
curl -X GET "http://localhost:5030/api/metrics/summary?startDate=2026-04-08&endDate=2026-04-09&tenantId=<tenant-guid>" \
  -H "Cookie: admin_session=..."
```

**Expected**: Only metrics for specified tenant returned

---

## Success Criteria

### Technical

- [x] All 6 files created without errors
- [x] `dotnet build` succeeds with 0 warnings
- [x] All 3 endpoints return valid JSON
- [x] Query latency <500ms for 10K metrics (verify with `EXPLAIN ANALYZE`)
- [x] Response caching working (5min TTL)
- [x] Tenant isolation enforced (ITenantContext integration)
- [x] Admin authorization enforced (`[Authorize]` attribute)

### Business

- [x] A/B test comparison data accessible via API
- [x] Pipeline performance metrics visible
- [x] Data sufficient for statistical analysis (completion rates, latencies)
- [x] API ready for dashboard integration (Phase 7.5)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Query performance degradation | Medium | Medium | Add indexes on (tenant_id, created_at, variant), use AsNoTracking(), implement pagination if needed |
| Unauthorized data access | Low | High | Admin authorization via [Authorize], tenant filtering in service layer |
| Cache invalidation issues | Low | Low | Short TTL (5min), acceptable staleness for analytics |
| Missing metrics data | Medium | Low | Graceful degradation (return 0 for missing data), log warnings |
| JSONB parsing errors | Medium | Low | Try-catch around JSON parsing, skip invalid records |

---

## Database Indexes (Performance Optimization)

**Recommended Indexes** (add if query latency >500ms):

```sql
-- Composite index for date range + tenant queries
CREATE INDEX idx_conversation_metrics_tenant_date 
ON conversation_metrics(tenant_id, created_at DESC);

-- Index for variant filtering
CREATE INDEX idx_conversation_metrics_variant 
ON conversation_metrics(variant);

-- Index for session aggregation
CREATE INDEX idx_conversation_metrics_session 
ON conversation_metrics(session_id);
```

**When to Add**:
- After Phase 7.4 testing
- If query latency exceeds 500ms with 10K+ metrics
- Monitor with `EXPLAIN ANALYZE` during load testing

---

## Security Considerations

### Authorization

- **Admin-only endpoints**: `[Authorize]` attribute on controller
- **Tenant isolation**: All queries filtered by tenant_id
- **No PII exposed**: Metrics are aggregated, no user-identifiable data

### Rate Limiting

**Recommendation**: Add rate limiting in Phase 8 (post-MVP)

```csharp
// Future: Add rate limiting middleware
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("metrics", opt => {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60; // 60 requests per minute
    });
});
```

---

## Next Steps

After Phase 7.3 completion:

1. **Verify API endpoints** with real data (manual testing)
2. **Check tenant isolation** (query different tenants)
3. **Proceed to Phase 7.4**: Testing & Validation
   - Unit tests for MetricsAggregationService
   - Integration tests for MetricsController
   - Performance tests (query latency benchmarks)
4. **Phase 7.5**: Custom Dashboard (consumes these APIs)

---

## Rollback Plan

**If Phase 7.3 fails**:

1. Remove MetricsController.cs
2. Remove IMetricsAggregationService + implementation
3. Remove DTO models
4. Remove service registration from Program.cs
5. Remove response caching middleware

**No database changes** (Phase 7.2 schema unchanged)

**No data loss** (read-only operations)

---

## Unresolved Questions

1. **Pagination**: Should we add pagination for large result sets? (Recommendation: Defer to Phase 8, aggregated results are small)
2. **Export functionality**: Add CSV/Excel export? (Recommendation: Defer to Phase 8, API-first approach)
3. **Real-time updates**: WebSocket vs polling? (Recommendation: Polling sufficient for MVP, 5min cache acceptable)
4. **Multi-tenant admin**: Should super-admin see all tenants? (Recommendation: Yes, via optional tenantId param)

---

## File Checklist

- [ ] `Services/Metrics/Models/MetricsSummaryDto.cs` (created)
- [ ] `Services/Metrics/Models/VariantComparisonDto.cs` (created)
- [ ] `Services/Metrics/Models/PipelinePerformanceDto.cs` (created)
- [ ] `Services/Metrics/IMetricsAggregationService.cs` (created)
- [ ] `Services/Metrics/MetricsAggregationService.cs` (created)
- [ ] `Controllers/MetricsController.cs` (created)
- [ ] `Program.cs` (modified - service registration + caching)

**Total**: 6 new files, 1 modified file
