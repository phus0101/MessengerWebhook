# Phase 4: Hybrid Orchestrator

**Priority:** P1  
**Status:** pending  
**Effort:** 2h  
**Dependencies:** Phase 2 (Keyword Detector), Phase 3 (Gemini Classifier)

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md` (lines 59-117)
- Pattern reference: `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`
- Phase 2: `phase-02-keyword-detector.md`
- Phase 3: `phase-03-gemini-classifier.md`

## Overview

Orchestrate keyword-first + AI fallback strategy. Routes 70% queries through fast keyword path (<10ms), escalates ambiguous 30% to AI (~510ms). Merges results based on confidence thresholds.

## Key Insights

- Keyword high confidence (≥0.9): accept immediately, skip AI
- Keyword medium confidence (0.6-0.9): run AI in parallel, prefer AI if ≥0.7
- Keyword low confidence (<0.6): run AI only
- AI timeout/error: fallback to keyword result or None
- Performance target: <500ms for 70% of queries

## Requirements

### Functional
- Try keyword detector first (fast path)
- Escalate to AI if keyword confidence <0.9
- Merge results: prefer AI if confidence ≥0.7
- Fallback to keyword on AI timeout/error
- Return source metadata (keyword, ai, hybrid)
- Support cancellation tokens

### Non-Functional
- Latency: p50 <100ms (keyword only), p95 <1s (hybrid)
- Accuracy: ≥85% (better than keyword-only 70%)
- Cost: ~$0.075/month (30% AI usage)
- Reliability: never fail (always return result or null)
- Thread-safe: stateless orchestration

## Architecture

```
HybridSubIntentClassifier
├── KeywordSubIntentDetector (injected)
├── GeminiSubIntentClassifier (injected)
├── SubIntentOptions (injected)
└── ClassifyAsync(message, context, cancellationToken)
    ├── Step 1: Try keyword detector (sync, <10ms)
    │   └── If confidence ≥0.9 → return immediately
    ├── Step 2: Try AI classifier (async, ~510ms)
    │   └── If timeout/error → fallback to keyword
    └── Step 3: Merge results
        ├── AI confidence ≥0.7 → use AI
        ├── AI confidence <0.7 → use keyword
        └── Both null → return null
```

**Decision Tree:**
```
User Message
    ↓
Keyword Detect
    ├─ confidence ≥0.9 → RETURN (fast path, 70%)
    ├─ confidence 0.6-0.9 → AI Classify
    │   ├─ AI confidence ≥0.7 → RETURN AI
    │   └─ AI confidence <0.7 → RETURN Keyword
    └─ confidence <0.6 → AI Classify
        ├─ AI success → RETURN AI
        └─ AI fail → RETURN Keyword or null
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Services/SubIntent/HybridSubIntentClassifier.cs`

**Dependencies:**
- `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/KeywordSubIntentDetector.cs` (Phase 2)
- `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs` (Phase 3)
- `src/MessengerWebhook/Configuration/SubIntentOptions.cs` (Phase 5)

## Implementation Steps

### 1. Create HybridSubIntentClassifier class (30min)

```csharp
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Hybrid sub-intent classifier: keyword-first with AI fallback
/// </summary>
public sealed class HybridSubIntentClassifier : ISubIntentClassifier
{
    private readonly KeywordSubIntentDetector _keywordDetector;
    private readonly GeminiSubIntentClassifier _aiClassifier;
    private readonly SubIntentOptions _options;
    private readonly ILogger<HybridSubIntentClassifier> _logger;

    public HybridSubIntentClassifier(
        KeywordSubIntentDetector keywordDetector,
        GeminiSubIntentClassifier aiClassifier,
        IOptions<SubIntentOptions> options,
        ILogger<HybridSubIntentClassifier> logger)
    {
        _keywordDetector = keywordDetector;
        _aiClassifier = aiClassifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        // Step 1: Try keyword detector (fast path)
        var keywordResult = await _keywordDetector.ClassifyAsync(message, conversationContext, cancellationToken);
        
        if (keywordResult != null && keywordResult.Confidence >= _options.KeywordHighConfidenceThreshold)
        {
            _logger.LogDebug(
                "Keyword detector high confidence: {Category} ({Confidence})",
                keywordResult.Category,
                keywordResult.Confidence);
            
            return keywordResult with { Source = "keyword" };
        }

        // Step 2: Try AI classifier (fallback for ambiguous cases)
        SubIntentResult? aiResult = null;
        try
        {
            aiResult = await _aiClassifier.ClassifyAsync(message, conversationContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI classifier failed, falling back to keyword result");
        }

        // Step 3: Merge results
        return MergeResults(keywordResult, aiResult);
    }

    private SubIntentResult? MergeResults(SubIntentResult? keywordResult, SubIntentResult? aiResult)
    {
        // AI succeeded with high confidence
        if (aiResult != null && aiResult.Confidence >= _options.MinConfidence)
        {
            _logger.LogInformation(
                "Using AI result: {Category} (confidence: {Confidence})",
                aiResult.Category,
                aiResult.Confidence);
            
            return aiResult with { Source = "ai" };
        }

        // AI failed or low confidence, use keyword result
        if (keywordResult != null)
        {
            _logger.LogInformation(
                "Using keyword result: {Category} (confidence: {Confidence}, AI confidence: {AiConfidence})",
                keywordResult.Category,
                keywordResult.Confidence,
                aiResult?.Confidence ?? 0);
            
            return keywordResult with { Source = "hybrid-keyword" };
        }

        // Both failed
        _logger.LogDebug("Both keyword and AI classifiers returned null");
        return null;
    }
}
```

### 2. Add performance logging (20min)

```csharp
public async Task<SubIntentResult?> ClassifyAsync(
    string message,
    ConversationContext? conversationContext = null,
    CancellationToken cancellationToken = default)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        // ... existing logic ...
        
        return result;
    }
    finally
    {
        stopwatch.Stop();
        _logger.LogInformation(
            "Sub-intent classification completed in {ElapsedMs}ms (source: {Source})",
            stopwatch.ElapsedMilliseconds,
            result?.Source ?? "none");
    }
}
```

### 3. Add metrics tracking (30min)

```csharp
private SubIntentResult? MergeResults(SubIntentResult? keywordResult, SubIntentResult? aiResult)
{
    // Track decision metrics
    var decision = (keywordResult != null, aiResult != null) switch
    {
        (true, true) when aiResult.Confidence >= _options.MinConfidence => "ai-preferred",
        (true, true) => "keyword-preferred",
        (true, false) => "keyword-only",
        (false, true) => "ai-only",
        _ => "both-failed"
    };
    
    _logger.LogDebug(
        "Merge decision: {Decision} (keyword: {KeywordConf}, ai: {AiConf})",
        decision,
        keywordResult?.Confidence ?? 0,
        aiResult?.Confidence ?? 0);
    
    // ... existing merge logic ...
}
```

### 4. Add circuit breaker pattern (optional, 40min)

```csharp
private int _consecutiveAiFailures = 0;
private DateTime? _aiDisabledUntil = null;
private readonly object _circuitLock = new();

private bool IsAiAvailable()
{
    lock (_circuitLock)
    {
        if (_aiDisabledUntil.HasValue && DateTime.UtcNow < _aiDisabledUntil.Value)
        {
            return false; // Circuit open
        }
        
        if (_aiDisabledUntil.HasValue && DateTime.UtcNow >= _aiDisabledUntil.Value)
        {
            // Reset circuit
            _consecutiveAiFailures = 0;
            _aiDisabledUntil = null;
            _logger.LogInformation("AI classifier circuit breaker reset");
        }
        
        return true;
    }
}

private void RecordAiFailure()
{
    lock (_circuitLock)
    {
        _consecutiveAiFailures++;
        
        if (_consecutiveAiFailures >= _options.CircuitBreakerThreshold)
        {
            _aiDisabledUntil = DateTime.UtcNow.AddMinutes(_options.CircuitBreakerCooldownMinutes);
            _logger.LogWarning(
                "AI classifier circuit breaker opened after {Failures} failures. Disabled until {Until}",
                _consecutiveAiFailures,
                _aiDisabledUntil);
        }
    }
}

private void RecordAiSuccess()
{
    lock (_circuitLock)
    {
        _consecutiveAiFailures = 0;
    }
}
```

## Todo List

- [ ] Create `HybridSubIntentClassifier.cs` class
- [ ] Implement `ClassifyAsync` with keyword-first logic
- [ ] Implement `MergeResults` with confidence-based decision
- [ ] Add performance logging (elapsed time per classification)
- [ ] Add metrics tracking (decision types: ai-preferred, keyword-only, etc.)
- [ ] Add circuit breaker pattern (optional, for production resilience)
- [ ] Add XML documentation
- [ ] Compile and verify no errors
- [ ] Create unit test stubs

## Success Criteria

- [ ] Routes high-confidence keyword results immediately (≥0.9)
- [ ] Escalates ambiguous cases to AI (<0.9)
- [ ] Merges results correctly: AI preferred if ≥0.7, else keyword
- [ ] Handles AI timeout/error gracefully (fallback to keyword)
- [ ] Logs performance metrics (elapsed time, source)
- [ ] Logs decision metrics (ai-preferred, keyword-only, etc.)
- [ ] Circuit breaker opens after N consecutive AI failures (optional)
- [ ] Thread-safe (no race conditions in circuit breaker)
- [ ] No exceptions leak to caller

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| AI always times out | Low | High | Circuit breaker disables AI, fallback to keyword |
| Keyword false positives | Medium | Medium | AI corrects with higher confidence |
| Performance regression | Low | Medium | Benchmark shows p95 <1s |
| Circuit breaker too aggressive | Low | Low | Tune threshold (default: 5 failures) |

## Security Considerations

- No PII stored (stateless orchestration)
- Circuit breaker state in-memory (not persisted)
- Cancellation tokens propagated correctly

## Next Steps

**Blocks:** Phase 5 (Configuration & DI)

**After completion:**
1. Benchmark performance (p50, p95, p99)
2. Tune confidence thresholds based on production data
3. Proceed to Phase 5: Configuration and DI setup
