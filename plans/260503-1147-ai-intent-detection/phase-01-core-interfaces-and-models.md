# Phase 1: Core Interfaces and Models

**Priority:** P1  
**Status:** pending  
**Effort:** 2h  
**Dependencies:** None

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md`
- Pattern reference: `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`
- Pattern reference: `src/MessengerWebhook/Services/Policy/PolicyClassificationResult.cs`

## Overview

Create foundational interfaces and models for sub-intent classification system. Establish contracts that all implementations (keyword, AI, hybrid) will follow.

## Key Insights

- Pattern proven in `GeminiPolicyIntentClassifier` - reuse structure
- Confidence scoring critical for hybrid decision-making
- Need extensible enum for sub-intent categories
- Result model must support debugging (matched keywords, explanation)

## Requirements

### Functional
- Define `ISubIntentClassifier` interface with async classification method
- Create `SubIntentCategory` enum with 7 categories
- Create `SubIntentResult` model with confidence, category, metadata
- Support cancellation tokens for timeout handling

### Non-Functional
- Interfaces must be mockable for unit testing
- Models must be JSON-serializable for logging
- Confidence range: 0.0-1.0 (decimal)
- Thread-safe, immutable result objects

## Architecture

```
Services/SubIntent/
├── ISubIntentClassifier.cs       # Core interface
├── SubIntentCategory.cs          # Enum (7 categories)
└── SubIntentResult.cs            # Result model
```

**Data Flow:**
```
User Message → ISubIntentClassifier.ClassifyAsync() → SubIntentResult
                                                      ├── Category (enum)
                                                      ├── Confidence (0-1)
                                                      ├── MatchedKeywords (string[])
                                                      └── Explanation (string)
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs`
- `src/MessengerWebhook/Services/SubIntent/SubIntentCategory.cs`
- `src/MessengerWebhook/Services/SubIntent/SubIntentResult.cs`

**Reference patterns:**
- `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs` (interface pattern)
- `src/MessengerWebhook/Services/Policy/PolicyClassificationResult.cs` (result model pattern)

## Implementation Steps

### 1. Create SubIntentCategory enum (15min)
```csharp
namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Sub-intent categories for customer questions in sales conversations
/// </summary>
public enum SubIntentCategory
{
    /// <summary>No specific sub-intent detected</summary>
    None = 0,
    
    /// <summary>Questions about product features, ingredients, usage</summary>
    ProductQuestion = 1,
    
    /// <summary>Questions about price, cost, discounts</summary>
    PriceQuestion = 2,
    
    /// <summary>Questions about delivery time, shipping cost, tracking</summary>
    ShippingQuestion = 3,
    
    /// <summary>Questions about return, refund, warranty policies</summary>
    PolicyQuestion = 4,
    
    /// <summary>Questions about stock availability</summary>
    AvailabilityQuestion = 5,
    
    /// <summary>Comparing multiple products</summary>
    ComparisonQuestion = 6
}
```

### 2. Create SubIntentResult model (30min)
```csharp
namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Result of sub-intent classification
/// </summary>
public sealed record SubIntentResult
{
    /// <summary>Detected sub-intent category</summary>
    public required SubIntentCategory Category { get; init; }
    
    /// <summary>Confidence score (0.0-1.0)</summary>
    public required decimal Confidence { get; init; }
    
    /// <summary>Keywords that matched (for keyword detector)</summary>
    public string[] MatchedKeywords { get; init; } = Array.Empty<string>();
    
    /// <summary>Human-readable explanation (for AI classifier)</summary>
    public string Explanation { get; init; } = string.Empty;
    
    /// <summary>Source of classification (keyword, ai, hybrid)</summary>
    public string Source { get; init; } = "unknown";
    
    /// <summary>Timestamp of classification</summary>
    public DateTime ClassifiedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>Create result with validation</summary>
    public static SubIntentResult Create(
        SubIntentCategory category,
        decimal confidence,
        string[] matchedKeywords,
        string explanation,
        string source)
    {
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be 0-1");
        
        return new SubIntentResult
        {
            Category = category,
            Confidence = confidence,
            MatchedKeywords = matchedKeywords ?? Array.Empty<string>(),
            Explanation = explanation ?? string.Empty,
            Source = source
        };
    }
}
```

### 3. Create ISubIntentClassifier interface (30min)
```csharp
namespace MessengerWebhook.Services.SubIntent;

/// <summary>
/// Classifies customer messages into sub-intent categories
/// </summary>
public interface ISubIntentClassifier
{
    /// <summary>
    /// Classify a customer message into a sub-intent category
    /// </summary>
    /// <param name="message">Customer message text</param>
    /// <param name="conversationContext">Optional conversation context for disambiguation</param>
    /// <param name="cancellationToken">Cancellation token for timeout</param>
    /// <returns>Classification result or null if unable to classify</returns>
    Task<SubIntentResult?> ClassifyAsync(
        string message,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Conversation context for intent classification
/// </summary>
public sealed record ConversationContext
{
    /// <summary>Current conversation state</summary>
    public string? CurrentState { get; init; }
    
    /// <summary>Whether customer has selected a product</summary>
    public bool HasProduct { get; init; }
    
    /// <summary>Recent conversation history (last 3-5 messages)</summary>
    public List<ConversationMessage> RecentHistory { get; init; } = new();
    
    /// <summary>Dominant topic from TopicAnalyzer</summary>
    public string? DominantTopic { get; init; }
}

/// <summary>
/// Conversation message for context
/// </summary>
public sealed record ConversationMessage
{
    public required string Role { get; init; } // "user" or "assistant"
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

### 4. Add XML documentation (15min)
- Document all public members
- Add usage examples in interface docs
- Document confidence threshold recommendations

### 5. Compile and verify (30min)
```bash
dotnet build src/MessengerWebhook
```

## Todo List

- [ ] Create `SubIntentCategory.cs` enum with 7 categories
- [ ] Create `SubIntentResult.cs` record with validation
- [ ] Create `ISubIntentClassifier.cs` interface
- [ ] Create `ConversationContext.cs` and `ConversationMessage.cs` records
- [ ] Add XML documentation to all public members
- [ ] Compile project - verify no errors
- [ ] Review with pattern reference files

## Success Criteria

- [ ] All 3 files compile without errors
- [ ] `SubIntentCategory` has 7 enum values (None + 6 question types)
- [ ] `SubIntentResult` validates confidence range (0-1)
- [ ] `ISubIntentClassifier` signature matches pattern from `IPolicyIntentClassifier`
- [ ] All public members have XML documentation
- [ ] Models are immutable (record types)
- [ ] No external dependencies (pure models/interfaces)

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Enum values don't cover all cases | Low | Medium | Start with 7 categories, extend later if needed |
| Confidence range validation missed | Low | High | Add validation in factory method |
| Interface too rigid for future needs | Medium | Medium | Keep interface minimal, extend via context object |
| ConversationContext too complex | Low | Low | Make all fields optional, start simple |

## Security Considerations

- No PII in models (message content passed as param, not stored)
- Confidence scores logged for debugging - ensure no sensitive data
- Cancellation tokens prevent resource exhaustion

## Next Steps

**Blocks:** Phase 2 (Keyword Detector)

**After completion:**
1. Review interfaces with team
2. Proceed to Phase 2: Keyword detector implementation
3. Create unit test stubs (actual tests in Phase 7)
