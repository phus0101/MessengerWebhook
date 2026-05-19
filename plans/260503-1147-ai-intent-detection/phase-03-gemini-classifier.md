# Phase 3: Gemini AI Classifier (Fallback)

**Priority:** P1  
**Status:** pending  
**Effort:** 3h  
**Dependencies:** Phase 1 (Core interfaces)

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md` (lines 125-163)
- Pattern reference: `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`
- Gemini service: `src/MessengerWebhook/Services/AI/IGeminiService.cs`
- Phase 1 interfaces: `phase-01-core-interfaces-and-models.md`

## Overview

Implement AI-based sub-intent classifier using Gemini Flash-Lite for ambiguous queries that keyword detector cannot handle confidently. Handles 30% of queries with ~510ms latency.

## Key Insights

- Pattern proven in `GeminiPolicyIntentClassifier` - reuse HTTP client, timeout, error handling
- Gemini Flash-Lite: 510ms TTFT, 94% intent accuracy, 97% structured output compliance
- Prompt engineering critical: few-shot examples, context injection, JSON schema
- Confidence calibration: LLMs overconfident (90% claimed → 70-85% actual)
- Timeout 500ms hard limit - fallback to keyword result on timeout

## Requirements

### Functional
- Classify sub-intent using Gemini Flash-Lite model
- Return confidence score (0-1) with explanation
- Support conversation context for disambiguation
- Handle timeout (500ms) gracefully
- Parse JSON response with error recovery
- Extract matched keywords from AI explanation

### Non-Functional
- Latency: p95 <1s (510ms TTFT + 400ms generation)
- Accuracy: ≥85% intent classification
- Cost: ~$0.000025 per query
- Reliability: fallback on API error, timeout, invalid JSON
- Thread-safe: stateless, no shared mutable state

## Architecture

```
GeminiSubIntentClassifier
├── HttpClient (injected, from GeminiOptions)
├── ClassifyAsync(message, context, cancellationToken)
│   ├── Build prompt with few-shot examples
│   ├── Inject conversation context
│   ├── POST to Gemini API (500ms timeout)
│   ├── Parse JSON response
│   ├── Validate confidence threshold
│   └── Return SubIntentResult
└── Error handling:
    ├── Timeout → return null (fallback to keyword)
    ├── HTTP error → log + return null
    ├── Invalid JSON → extract + retry or return null
    └── Low confidence (<0.5) → return null
```

**Data Flow:**
```
User Message + Context
    ↓
Build Prompt (few-shot + context injection)
    ↓
POST /v1beta/models/gemini-2.5-flash-lite:generateContent
    ↓ (500ms timeout)
Gemini Response (JSON)
    ↓
Parse & Validate
    ↓
SubIntentResult(category, confidence, explanation, "ai")
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs`

**Dependencies:**
- `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/SubIntentResult.cs` (Phase 1)
- `src/MessengerWebhook/Configuration/GeminiOptions.cs` (existing)
- `System.Net.Http.HttpClient` (injected)

**Reference pattern:**
- `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs` (lines 31-77)

## Implementation Steps

### 1. Create GeminiSubIntentClassifier class (30min)

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessengerWebhook.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.SubIntent;

public sealed class GeminiSubIntentClassifier : ISubIntentClassifier
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _geminiOptions;
    private readonly SubIntentOptions _subIntentOptions;
    private readonly ILogger<GeminiSubIntentClassifier> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeminiSubIntentClassifier(
        HttpClient httpClient,
        IOptions<GeminiOptions> geminiOptions,
        IOptions<SubIntentOptions> subIntentOptions,
        ILogger<GeminiSubIntentClassifier> logger)
    {
        _httpClient = httpClient;
        _geminiOptions = geminiOptions.Value;
        _subIntentOptions = subIntentOptions.Value;
        _logger = logger;
    }

    public async Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_subIntentOptions.ClassifierTimeoutMs));

        try
        {
            var model = string.IsNullOrWhiteSpace(_geminiOptions.FlashLiteModel)
                ? _geminiOptions.ProModel
                : _geminiOptions.FlashLiteModel;
            
            var url = $"v1beta/models/{model}:generateContent";
            if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
            {
                url += $"?key={Uri.EscapeDataString(_geminiOptions.ApiKey)}";
            }

            var request = BuildRequest(message, conversationContext);
            var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, timeoutCts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini sub-intent classifier returned HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(JsonOptions, timeoutCts.Token);
            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var classification = JsonSerializer.Deserialize<GeminiClassifierResponse>(ExtractJson(text), JsonOptions);
            
            if (classification == null || classification.Confidence < _subIntentOptions.MinConfidence)
            {
                return null;
            }

            return MapToSubIntentResult(classification);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Gemini sub-intent classifier failed");
            return null;
        }
    }
}
```

### 2. Build prompt with few-shot examples (45min)

```csharp
private object BuildRequest(string message, ConversationContext? context)
{
    var prompt = BuildPrompt(message, context);
    
    return new
    {
        contents = new[]
        {
            new
            {
                parts = new[]
                {
                    new { text = prompt }
                }
            }
        },
        generationConfig = new
        {
            temperature = 0.1,  // Deterministic
            maxOutputTokens = 150
        }
    };
}

private string BuildPrompt(string message, ConversationContext? context)
{
    var contextInfo = context != null
        ? $"Context: State={context.CurrentState}, HasProduct={context.HasProduct}, DominantTopic={context.DominantTopic}"
        : "Context: None";
    
    var historyContext = context?.RecentHistory?.Count > 0
        ? BuildHistoryContext(context.RecentHistory)
        : "No recent history";

    return $@"You are a Vietnamese e-commerce intent classifier.

Customer message: ""{message}""
{contextInfo}

Recent conversation:
{historyContext}

Task: Classify customer sub-intent into ONE category:
- product_question: asking about product features, ingredients, usage
- price_question: asking about price, cost, discounts
- shipping_question: asking about delivery time, shipping cost, tracking
- policy_question: asking about return, refund, warranty policies
- availability_question: asking if product is in stock
- comparison_question: comparing multiple products
- none: no specific sub-intent detected

IMPORTANT: Return ONLY valid JSON:
{{
  ""subIntent"": ""product_question|price_question|shipping_question|policy_question|availability_question|comparison_question|none"",
  ""confidence"": 0.0-1.0,
  ""reason"": ""brief explanation in English"",
  ""matchedKeywords"": [""keyword1"", ""keyword2""]
}}

Few-shot examples:
- ""sản phẩm này có chứa paraben không?"" → product_question (asking about ingredients)
- ""giá bao nhiêu vậy?"" → price_question (direct price inquiry)
- ""ship mất bao lâu?"" → shipping_question (delivery time)
- ""có freeship không?"" → shipping_question (shipping cost)
- ""còn hàng không?"" → availability_question (stock inquiry)
- ""em muốn mua combo"" → none (buying intent, not a question)
- ""ok"" (after bot asked about shipping) → none (affirmation, context-dependent)";
}

private string BuildHistoryContext(List<ConversationMessage> history)
{
    var recent = history.TakeLast(3);
    var lines = recent.Select(m => $"{m.Role}: {m.Content}");
    return string.Join("\n", lines);
}
```

### 3. Parse and validate JSON response (30min)

```csharp
private static string ExtractJson(string text)
{
    var trimmed = text.Trim();
    if (!trimmed.StartsWith("```", StringComparison.Ordinal))
    {
        return trimmed;
    }

    var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
    if (end <= 3)
    {
        return trimmed;
    }

    var body = trimmed[3..end].Trim();
    if (body.StartsWith("json", StringComparison.OrdinalIgnoreCase))
    {
        body = body[4..].Trim();
    }

    return body;
}

private static SubIntentResult? MapToSubIntentResult(GeminiClassifierResponse response)
{
    var category = response.SubIntent?.Trim().ToLowerInvariant() switch
    {
        "product_question" => SubIntentCategory.ProductQuestion,
        "price_question" => SubIntentCategory.PriceQuestion,
        "shipping_question" => SubIntentCategory.ShippingQuestion,
        "policy_question" => SubIntentCategory.PolicyQuestion,
        "availability_question" => SubIntentCategory.AvailabilityQuestion,
        "comparison_question" => SubIntentCategory.ComparisonQuestion,
        _ => SubIntentCategory.None
    };

    if (category == SubIntentCategory.None && response.SubIntent != "none")
    {
        return null; // Invalid category
    }

    return SubIntentResult.Create(
        category: category,
        confidence: response.Confidence,
        matchedKeywords: response.MatchedKeywords ?? Array.Empty<string>(),
        explanation: response.Reason ?? string.Empty,
        source: "ai");
}
```

### 4. Add response DTOs (15min)

```csharp
private sealed class GeminiClassifierResponse
{
    public string? SubIntent { get; set; }
    public decimal Confidence { get; set; }
    public string? Reason { get; set; }
    public string[]? MatchedKeywords { get; set; }
}

private sealed class GeminiGenerateContentResponse
{
    public GeminiCandidate[]? Candidates { get; set; }
}

private sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

private sealed class GeminiContent
{
    public GeminiPart[]? Parts { get; set; }
}

private sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
```

### 5. Add logging and metrics (30min)

```csharp
_logger.LogDebug("Classifying sub-intent for message: {Message}", message);

// After successful classification
_logger.LogInformation(
    "Sub-intent classified: {Category} (confidence: {Confidence}, source: ai)",
    result.Category,
    result.Confidence);

// On timeout
_logger.LogWarning("Gemini sub-intent classifier timed out after {TimeoutMs}ms", _subIntentOptions.ClassifierTimeoutMs);

// On low confidence
_logger.LogDebug("Gemini confidence {Confidence} below threshold {Threshold}", classification.Confidence, _subIntentOptions.MinConfidence);
```

## Todo List

- [ ] Create `GeminiSubIntentClassifier.cs` class
- [ ] Implement `ClassifyAsync` with timeout handling
- [ ] Implement `BuildRequest` and `BuildPrompt` methods
- [ ] Add few-shot examples (6 examples covering edge cases)
- [ ] Implement conversation context injection
- [ ] Implement `ExtractJson` for markdown-wrapped responses
- [ ] Implement `MapToSubIntentResult` with validation
- [ ] Add response DTOs (GeminiClassifierResponse, etc.)
- [ ] Add comprehensive logging (debug, info, warning)
- [ ] Add XML documentation
- [ ] Compile and verify no errors

## Success Criteria

- [ ] Classifies all 6 sub-intent categories correctly
- [ ] Returns confidence scores from Gemini (0-1 range)
- [ ] Handles 500ms timeout gracefully (returns null)
- [ ] Handles API errors gracefully (logs + returns null)
- [ ] Handles invalid JSON gracefully (extracts or returns null)
- [ ] Injects conversation context for disambiguation
- [ ] Few-shot examples cover edge cases (affirmations, multi-intent)
- [ ] Latency p95 <1s (measured in Phase 7)
- [ ] No exceptions leak to caller (all caught and logged)

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Gemini API timeout | Medium | Medium | 500ms timeout, fallback to keyword result |
| Invalid JSON response | Low | Low | Extract JSON from markdown, retry logic |
| Overconfident predictions | Medium | Medium | Threshold at 0.7, log for calibration |
| API rate limiting | Low | High | Respect rate limits in GeminiOptions, circuit breaker |
| Cost overrun | Low | Low | 30% usage cap, monitor daily spend |

## Security Considerations

- API key passed via query param (existing pattern)
- No PII logged (message content at debug level only)
- Timeout prevents resource exhaustion
- Cancellation token propagated correctly

## Next Steps

**Blocks:** Phase 4 (Hybrid Orchestrator)

**After completion:**
1. Test with real Vietnamese queries (manual validation)
2. Calibrate confidence threshold (may need adjustment from 0.7)
3. Proceed to Phase 4: Hybrid orchestrator
