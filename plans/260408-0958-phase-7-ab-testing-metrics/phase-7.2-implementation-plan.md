---
title: "Phase 7.2: Metrics Collection Service - Implementation Plan"
description: "Detailed implementation plan for async metrics collection with batch writes"
status: pending
priority: P1
effort: 5h
dependencies: [Phase 7.1]
created: 2026-04-08
---

# Phase 7.2: Metrics Collection Service - Implementation Plan

## Executive Summary

Build async metrics collection system with <10ms overhead, batch writes (100 metrics/60s), and zero impact on user response time. Tracks naturalness pipeline performance for A/B test analysis.

**Key Architecture Decisions:**
- ConcurrentQueue for thread-safe in-memory buffer
- Background service with dual flush triggers (count + time)
- JSONB storage for schema flexibility
- Tenant isolation via ITenantOwnedEntity + global query filters

---

## Data Flow Architecture

```
Message Processing (SalesStateHandlerBase)
    ↓
Extract metrics (emotion, tone, latency, variant)
    ↓
ConversationMetricsService.LogAsync() [<10ms]
    ↓
Add to ConcurrentQueue (in-memory buffer)
    ↓
MetricsBackgroundService monitors queue
    ↓
Flush trigger: 100 items OR 60s elapsed
    ↓
Batch INSERT to conversation_metrics table
    ↓
Clear buffer, repeat
```

**Control Group:** Logs basic metrics (latency, variant, outcome) - pipeline fields NULL
**Treatment Group:** Logs full metrics (emotion, tone, journey stage, validation results)

---

## Database Schema

### Table: conversation_metrics

```sql
CREATE TABLE conversation_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    session_id UUID NOT NULL,
    facebook_psid VARCHAR(255) NOT NULL,
    ab_test_variant VARCHAR(50) NOT NULL, -- 'control' or 'treatment'
    
    -- Message context
    message_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    conversation_turn INT NOT NULL,
    
    -- Performance metrics (both variants)
    total_response_time_ms INT NOT NULL,
    pipeline_latency_ms INT NULL, -- NULL for control
    
    -- Pipeline metrics (treatment only)
    detected_emotion VARCHAR(50) NULL,
    emotion_confidence DECIMAL(3,2) NULL,
    matched_tone VARCHAR(50) NULL,
    journey_stage VARCHAR(50) NULL,
    validation_passed BOOLEAN NULL,
    validation_errors JSONB NULL,
    
    -- Conversation outcome
    conversation_outcome VARCHAR(50) NULL, -- 'completed', 'abandoned', 'escalated'
    
    -- Flexible storage for future metrics
    additional_metrics JSONB NULL,
    
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_conversation_metrics_tenant 
        FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    CONSTRAINT fk_conversation_metrics_session 
        FOREIGN KEY (session_id) REFERENCES conversation_sessions(id) ON DELETE CASCADE
);

-- Indexes for query performance
CREATE INDEX idx_conversation_metrics_tenant_id ON conversation_metrics(tenant_id);
CREATE INDEX idx_conversation_metrics_session_id ON conversation_metrics(session_id);
CREATE INDEX idx_conversation_metrics_variant ON conversation_metrics(ab_test_variant);
CREATE INDEX idx_conversation_metrics_timestamp ON conversation_metrics(message_timestamp);
CREATE INDEX idx_conversation_metrics_outcome ON conversation_metrics(conversation_outcome) WHERE conversation_outcome IS NOT NULL;

-- GIN index for JSONB queries
CREATE INDEX idx_conversation_metrics_additional_metrics ON conversation_metrics USING GIN(additional_metrics);
```

---

## Implementation Steps

### Step 1: Create Entity Model (30min)

**File:** `src/MessengerWebhook/Data/Entities/ConversationMetric.cs`

```csharp
using System.Text.Json;

namespace MessengerWebhook.Data.Entities;

public class ConversationMetric : ITenantOwnedEntity
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string ABTestVariant { get; set; } = string.Empty;
    
    // Message context
    public DateTime MessageTimestamp { get; set; }
    public int ConversationTurn { get; set; }
    
    // Performance metrics
    public int TotalResponseTimeMs { get; set; }
    public int? PipelineLatencyMs { get; set; }
    
    // Pipeline metrics (treatment only)
    public string? DetectedEmotion { get; set; }
    public decimal? EmotionConfidence { get; set; }
    public string? MatchedTone { get; set; }
    public string? JourneyStage { get; set; }
    public bool? ValidationPassed { get; set; }
    public JsonDocument? ValidationErrors { get; set; }
    
    // Conversation outcome
    public string? ConversationOutcome { get; set; }
    
    // Flexible storage
    public JsonDocument? AdditionalMetrics { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ConversationSession? Session { get; set; }
    public Tenant? Tenant { get; set; }
}
```

**Why JsonDocument?** EF Core maps `JsonDocument` to PostgreSQL JSONB natively. Provides type-safe access with flexibility.

---

### Step 2: Create Service Models (20min)

**File:** `src/MessengerWebhook/Services/Metrics/Models/ConversationMetricData.cs`

```csharp
namespace MessengerWebhook.Services.Metrics.Models;

public class ConversationMetricData
{
    public required Guid SessionId { get; init; }
    public required string FacebookPSID { get; init; }
    public required string ABTestVariant { get; init; }
    
    // Message context
    public required DateTime MessageTimestamp { get; init; }
    public required int ConversationTurn { get; init; }
    
    // Performance metrics
    public required int TotalResponseTimeMs { get; init; }
    public int? PipelineLatencyMs { get; init; }
    
    // Pipeline metrics (treatment only)
    public string? DetectedEmotion { get; init; }
    public decimal? EmotionConfidence { get; init; }
    public string? MatchedTone { get; init; }
    public string? JourneyStage { get; init; }
    public bool? ValidationPassed { get; init; }
    public Dictionary<string, object>? ValidationErrors { get; init; }
    
    // Conversation outcome
    public string? ConversationOutcome { get; init; }
    
    // Flexible storage
    public Dictionary<string, object>? AdditionalMetrics { get; init; }
}
```

---

### Step 3: Create Service Interface (10min)

**File:** `src/MessengerWebhook/Services/Metrics/IConversationMetricsService.cs`

```csharp
using MessengerWebhook.Services.Metrics.Models;

namespace MessengerWebhook.Services.Metrics;

public interface IConversationMetricsService
{
    /// <summary>
    /// Logs conversation metrics asynchronously (non-blocking, <10ms).
    /// Metrics are buffered and flushed in batches.
    /// </summary>
    Task LogAsync(ConversationMetricData metricData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets current buffer size (for monitoring/debugging).
    /// </summary>
    int GetBufferSize();
    
    /// <summary>
    /// Forces immediate flush of buffered metrics (for testing/shutdown).
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
```

---

### Step 4: Implement Service with Buffer (60min)

**File:** `src/MessengerWebhook/Services/Metrics/ConversationMetricsService.cs`

```csharp
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
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ConversationMetricsService> _logger;
    private readonly ConcurrentQueue<ConversationMetricData> _metricsBuffer;
    
    public ConversationMetricsService(
        IServiceScopeFactory scopeFactory,
        ITenantContext tenantContext,
        ILogger<ConversationMetricsService> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
        _logger = logger;
        _metricsBuffer = new ConcurrentQueue<ConversationMetricData>();
    }
    
    public Task LogAsync(ConversationMetricData metricData, CancellationToken cancellationToken = default)
    {
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
            
            var entities = batch.Select(m => new ConversationMetric
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
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
                _tenantContext.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} metrics to database", batch.Count);
            
            // Re-enqueue failed metrics (simple retry strategy)
            foreach (var metric in batch)
            {
                _metricsBuffer.Enqueue(metric);
            }
            
            throw;
        }
    }
}
```

**Key Design Decisions:**
- `ConcurrentQueue<T>`: Thread-safe, lock-free queue for high-throughput scenarios
- `IServiceScopeFactory`: Creates new DbContext scope for background writes (avoids lifetime issues)
- Simple retry: Re-enqueue on failure (background service will retry on next flush)

---

### Step 5: Create Background Service (45min)

**File:** `src/MessengerWebhook/Services/Metrics/MetricsBackgroundService.cs`

```csharp
using MessengerWebhook.Services.Metrics.Configuration;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.Metrics;

public class MetricsBackgroundService : BackgroundService
{
    private readonly IConversationMetricsService _metricsService;
    private readonly MetricsOptions _options;
    private readonly ILogger<MetricsBackgroundService> _logger;
    
    public MetricsBackgroundService(
        IConversationMetricsService metricsService,
        IOptions<MetricsOptions> options,
        ILogger<MetricsBackgroundService> logger)
    {
        _metricsService = metricsService;
        _options = options.Value;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Metrics background service started (Batch size: {BatchSize}, Flush interval: {FlushInterval}s)",
            _options.BatchSize,
            _options.FlushIntervalSeconds);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for flush interval
                await Task.Delay(TimeSpan.FromSeconds(_options.FlushIntervalSeconds), stoppingToken);
                
                var bufferSize = _metricsService.GetBufferSize();
                
                // Flush if buffer has items (either reached batch size or time elapsed)
                if (bufferSize > 0)
                {
                    _logger.LogDebug("Flushing metrics buffer (size: {BufferSize})", bufferSize);
                    await _metricsService.FlushAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                _logger.LogInformation("Metrics background service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics background service");
                // Continue running despite errors
            }
        }
        
        // Final flush on shutdown
        try
        {
            var remainingMetrics = _metricsService.GetBufferSize();
            if (remainingMetrics > 0)
            {
                _logger.LogInformation("Flushing {Count} remaining metrics on shutdown", remainingMetrics);
                await _metricsService.FlushAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics on shutdown");
        }
        
        _logger.LogInformation("Metrics background service stopped");
    }
}
```

**Shutdown Behavior:** Final flush ensures no metrics lost on graceful shutdown.

---

### Step 6: Create Configuration (15min)

**File:** `src/MessengerWebhook/Services/Metrics/Configuration/MetricsOptions.cs`

```csharp
namespace MessengerWebhook.Services.Metrics.Configuration;

public class MetricsOptions
{
    public const string SectionName = "Metrics";
    
    /// <summary>
    /// Maximum number of metrics to buffer before forcing a flush.
    /// Default: 100
    /// </summary>
    public int BatchSize { get; set; } = 100;
    
    /// <summary>
    /// Flush interval in seconds (even if batch size not reached).
    /// Default: 60 seconds
    /// </summary>
    public int FlushIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// Enable metrics collection.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
```

---

### Step 7: Update DbContext (20min)

**File:** `src/MessengerWebhook/Data/MessengerBotDbContext.cs`

**Add DbSet:**
```csharp
public DbSet<ConversationMetric> ConversationMetrics { get; set; }
```

**Add to OnModelCreating (after existing configurations):**
```csharp
// ConversationMetric configuration
modelBuilder.Entity<ConversationMetric>()
    .HasIndex(m => m.TenantId);

modelBuilder.Entity<ConversationMetric>()
    .HasIndex(m => m.SessionId);

modelBuilder.Entity<ConversationMetric>()
    .HasIndex(m => m.ABTestVariant);

modelBuilder.Entity<ConversationMetric>()
    .HasIndex(m => m.MessageTimestamp);

modelBuilder.Entity<ConversationMetric>()
    .HasIndex(m => m.ConversationOutcome)
    .HasFilter("conversation_outcome IS NOT NULL");

// JSONB columns
modelBuilder.Entity<ConversationMetric>()
    .Property(m => m.ValidationErrors)
    .HasColumnType("jsonb");

modelBuilder.Entity<ConversationMetric>()
    .Property(m => m.AdditionalMetrics)
    .HasColumnType("jsonb");

// Relationships
modelBuilder.Entity<ConversationMetric>()
    .HasOne(m => m.Session)
    .WithMany()
    .HasForeignKey(m => m.SessionId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<ConversationMetric>()
    .HasOne(m => m.Tenant)
    .WithMany()
    .HasForeignKey(m => m.TenantId)
    .OnDelete(DeleteBehavior.Cascade);
```

---

### Step 8: Create Migration (15min)

```bash
dotnet ef migrations add AddConversationMetrics --project src/MessengerWebhook
```

**Verify migration file includes:**
- Table creation with all columns
- Indexes (tenant_id, session_id, variant, timestamp, outcome)
- Foreign key constraints
- JSONB column types

**Apply migration:**
```bash
dotnet ef database update --project src/MessengerWebhook
```

---

### Step 9: Integrate into SalesStateHandlerBase (90min)

**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

**Step 9.1: Add service to constructor**

```csharp
protected readonly IConversationMetricsService ConversationMetricsService;

protected SalesStateHandlerBase(
    // ... existing parameters ...
    IConversationMetricsService conversationMetricsService,
    // ... rest of parameters ...
)
{
    // ... existing assignments ...
    ConversationMetricsService = conversationMetricsService;
    // ... rest of assignments ...
}
```

**Step 9.2: Add metrics capture in BuildNaturalReplyAsync**

Find the section after variant assignment (around line 510) and add metrics tracking:

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null)
{
    var startTime = DateTime.UtcNow;
    var pipelineStartTime = DateTime.UtcNow;
    
    // A/B Test: Check variant assignment
    var variant = await ABTestService.GetVariantAsync(ctx.FacebookPSID, ctx.SessionId, CancellationToken.None);
    ctx.SetData("abTestVariant", variant);

    Logger.LogInformation(
        "A/B Test variant for PSID {PSID}: {Variant} (Enabled: {Enabled})",
        ctx.FacebookPSID,
        variant,
        ABTestService.IsEnabled());

    // Control group: Skip naturalness pipeline, use direct AI response
    if (variant == "control")
    {
        Logger.LogInformation("Control group: Skipping naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);
        var response = await GenerateDirectAIResponseAsync(ctx, message, intent);
        
        // Log control metrics (no pipeline data)
        await LogMetricsAsync(ctx, startTime, null, null, null, null, null, null);
        
        return response;
    }

    // Treatment group: Run full naturalness pipeline
    Logger.LogInformation("Treatment group: Running full naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);
    
    // ... existing pipeline code (emotion detection, tone matching, etc.) ...
    
    // After pipeline completes, capture metrics
    var pipelineLatency = (int)(DateTime.UtcNow - pipelineStartTime).TotalMilliseconds;
    
    // ... generate response ...
    
    // Log treatment metrics (with pipeline data)
    await LogMetricsAsync(
        ctx,
        startTime,
        pipelineLatency,
        emotion?.Emotion,
        emotion?.Confidence,
        toneProfile?.PronounText,
        conversationContext?.CurrentStage.ToString(),
        validationResult
    );
    
    return finalResponse;
}
```

**Step 9.3: Add helper method for metrics logging**

```csharp
private async Task LogMetricsAsync(
    StateContext ctx,
    DateTime startTime,
    int? pipelineLatencyMs,
    string? detectedEmotion,
    decimal? emotionConfidence,
    string? matchedTone,
    string? journeyStage,
    ResponseValidationResult? validationResult)
{
    try
    {
        var totalResponseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        var history = GetHistory(ctx);
        var variant = ctx.GetData<string>("abTestVariant") ?? "control";
        
        var metricData = new ConversationMetricData
        {
            SessionId = ctx.SessionId,
            FacebookPSID = ctx.FacebookPSID,
            ABTestVariant = variant,
            MessageTimestamp = DateTime.UtcNow,
            ConversationTurn = history.Count,
            TotalResponseTimeMs = totalResponseTime,
            PipelineLatencyMs = pipelineLatencyMs,
            DetectedEmotion = detectedEmotion,
            EmotionConfidence = emotionConfidence,
            MatchedTone = matchedTone,
            JourneyStage = journeyStage,
            ValidationPassed = validationResult?.IsValid,
            ValidationErrors = validationResult?.Errors?.Any() == true
                ? validationResult.Errors.ToDictionary(e => e.Type.ToString(), e => (object)e.Message)
                : null,
            ConversationOutcome = null // Set later when conversation ends
        };
        
        await ConversationMetricsService.LogAsync(metricData);
        
        Logger.LogDebug(
            "Metrics logged - PSID: {PSID}, Variant: {Variant}, Latency: {Latency}ms",
            ctx.FacebookPSID,
            variant,
            totalResponseTime);
    }
    catch (Exception ex)
    {
        // Never fail user request due to metrics logging
        Logger.LogError(ex, "Failed to log metrics for PSID: {PSID}", ctx.FacebookPSID);
    }
}
```

**Critical:** Metrics logging wrapped in try-catch to prevent user-facing failures.

---

### Step 10: Update All StateHandler Subclasses (30min)

**Files to update:**
- `ConsultingStateHandler.cs`
- `CollectingInfoStateHandler.cs`
- `ConfirmingOrderStateHandler.cs`
- `ClosedStateHandler.cs`
- `ErrorStateHandler.cs`

**For each file, update constructor:**

```csharp
public ConsultingStateHandler(
    // ... existing parameters ...
    IConversationMetricsService conversationMetricsService,
    // ... rest of parameters ...
) : base(
    // ... existing parameters ...
    conversationMetricsService,
    // ... rest of parameters ...
)
{
}
```

**Repeat for all 5 handlers.**

---

### Step 11: Register Services in Program.cs (15min)

**File:** `src/MessengerWebhook/Program.cs`

**Add configuration:**
```csharp
builder.Services.Configure<MetricsOptions>(
    builder.Configuration.GetSection(MetricsOptions.SectionName));
```

**Add service registrations (after ABTesting services):**
```csharp
// Metrics Collection
builder.Services.AddSingleton<IConversationMetricsService, ConversationMetricsService>();
builder.Services.AddHostedService<MetricsBackgroundService>();
```

---

### Step 12: Add Configuration to appsettings.json (10min)

**File:** `src/MessengerWebhook/appsettings.json`

**Add after ABTesting section:**
```json
"Metrics": {
  "BatchSize": 100,
  "FlushIntervalSeconds": 60,
  "Enabled": true
}
```

---

### Step 13: Compile and Verify (30min)

```bash
# Build solution
dotnet build

# Check for compilation errors
# Fix any missing using statements or constructor mismatches

# Run migrations
dotnet ef database update --project src/MessengerWebhook

# Verify table created
psql -h localhost -p 5433 -U postgres -d messenger_bot -c "\d conversation_metrics"
```

**Expected output:** Table schema with all columns, indexes, and constraints.

---

## Testing Strategy

### Manual Testing Checklist

**Test 1: Control Group Metrics**
1. Set `ABTesting.Enabled = true`, `TreatmentPercentage = 0` (force control)
2. Send test message via webhook
3. Query database: `SELECT * FROM conversation_metrics ORDER BY created_at DESC LIMIT 1;`
4. Verify:
   - `ab_test_variant = 'control'`
   - `pipeline_latency_ms IS NULL`
   - `detected_emotion IS NULL`
   - `total_response_time_ms > 0`

**Test 2: Treatment Group Metrics**
1. Set `TreatmentPercentage = 100` (force treatment)
2. Send test message
3. Query database
4. Verify:
   - `ab_test_variant = 'treatment'`
   - `pipeline_latency_ms IS NOT NULL`
   - `detected_emotion IS NOT NULL`
   - `matched_tone IS NOT NULL`
   - `journey_stage IS NOT NULL`

**Test 3: Batch Flush (Time-based)**
1. Set `FlushIntervalSeconds = 10`
2. Send 5 messages (< batch size)
3. Wait 10 seconds
4. Query database: Should see 5 records

**Test 4: Batch Flush (Count-based)**
1. Set `BatchSize = 5`
2. Send 5 messages rapidly
3. Query immediately: Should see 5 records (no wait)

**Test 5: Tenant Isolation**
1. Send messages from 2 different tenants
2. Query with tenant filter: Each tenant sees only their metrics

**Test 6: Performance Impact**
1. Enable detailed logging
2. Send message, check logs for "Metrics logged" timing
3. Verify: Metrics logging < 10ms

---

## Risk Mitigation

| Risk | Mitigation | Verification |
|------|-----------|--------------|
| Memory leak from unbounded buffer | ConcurrentQueue with periodic flush (60s max) | Monitor buffer size via `GetBufferSize()` |
| Database write contention | Batch writes (100 items), async logging | Check DB connection pool usage |
| Metrics loss on crash | Acceptable for analytics (not transactional) | Document in system architecture |
| User request blocked by metrics | Try-catch wrapper, async logging | Load test: verify <10ms overhead |
| Tenant data leakage | ITenantOwnedEntity + global query filters | Test: query metrics across tenants |

---

## Rollback Plan

**If metrics cause production issues:**

1. **Immediate:** Set `Metrics.Enabled = false` in appsettings.json (restart app)
2. **Code rollback:** Remove metrics logging calls from `SalesStateHandlerBase`
3. **Database rollback:** 
   ```bash
   dotnet ef migrations remove --project src/MessengerWebhook
   dotnet ef database update --project src/MessengerWebhook
   ```

**No data loss:** Metrics are analytics data, not transactional. Safe to drop table.

---

## Success Criteria

**Technical:**
- ✅ Metrics service compiles without errors
- ✅ Migration applied successfully
- ✅ Metrics logged for 100% of messages
- ✅ Batch flush working (60s interval or 100 items)
- ✅ Async logging <10ms overhead
- ✅ No blocking on user-facing requests
- ✅ Tenant isolation maintained

**Business:**
- ✅ Metrics visible in database for both variants
- ✅ Control metrics have NULL pipeline fields
- ✅ Treatment metrics have full pipeline data
- ✅ Performance metrics captured accurately

---

## Next Steps

After Phase 7.2 completion:
1. Verify metrics logging in production (check logs + database)
2. Monitor memory usage and flush performance (Grafana/logs)
3. Proceed to **Phase 7.3: Metrics API & Reporting**
4. Build admin dashboard to visualize A/B test results

---

## Unresolved Questions

1. **Metrics retention:** 90 days or 180 days? (Recommendation: 90 days, add cleanup job in Phase 7.3)
2. **Real-time dashboard:** WebSocket updates or polling? (Defer to Phase 7.3)
3. **CSAT collection:** Add post-conversation survey? (Defer to Phase 8 - User Feedback)
