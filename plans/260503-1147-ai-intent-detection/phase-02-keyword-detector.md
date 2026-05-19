# Phase 2: Keyword Detector (Fast Path)

**Priority:** P1  
**Status:** pending  
**Effort:** 2h  
**Dependencies:** Phase 1 (Core interfaces)

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md` (lines 220-233)
- Existing keyword system: `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs`
- Phase 1 interfaces: `phase-01-core-interfaces-and-models.md`

## Overview

Implement fast keyword-based sub-intent detector (<10ms latency). Handles 70% of queries with high confidence (≥0.9) before escalating to AI.

## Key Insights

- Existing `TopicAnalyzer` has 6 topic categories with keyword lists
- Simple `Contains()` matching - no fuzzy logic, no context awareness
- Need confidence scoring based on keyword match strength
- Vietnamese-specific: handle informal spelling ("ko" vs "không")
- Multi-keyword matches increase confidence

## Requirements

### Functional
- Detect 6 sub-intent categories via keyword matching
- Return confidence score based on match strength
- Support Vietnamese informal text variations
- Handle multi-keyword matches (boost confidence)
- Return matched keywords for debugging
- <10ms latency (synchronous, no I/O)

### Non-Functional
- Thread-safe (stateless, immutable keyword dictionaries)
- Zero external dependencies (pure in-memory matching)
- Case-insensitive matching
- No regex (performance overhead)

## Architecture

```
KeywordSubIntentDetector
├── Keyword dictionaries (7 categories)
├── Detect(message) → SubIntentResult?
│   ├── Normalize message (lowercase, trim)
│   ├── Match keywords per category
│   ├── Calculate confidence (match count / total keywords)
│   └── Return highest confidence result
└── Confidence calculation:
    - Single keyword match: 0.6
    - 2+ keywords: 0.8
    - 3+ keywords: 0.9
```

**Data Flow:**
```
User Message
    ↓ (normalize)
"giá bao nhiêu vậy?"
    ↓ (match keywords)
["giá", "bao nhiêu"] → PriceQuestion
    ↓ (calculate confidence)
2 matches → 0.8 confidence
    ↓
SubIntentResult(PriceQuestion, 0.8, ["giá", "bao nhiêu"], "keyword")
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Services/SubIntent/KeywordSubIntentDetector.cs`

**Reference:**
- `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs` (keyword lists)

**Dependencies:**
- `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/SubIntentResult.cs` (Phase 1)
- `src/MessengerWebhook/Services/SubIntent/SubIntentCategory.cs` (Phase 1)

## Implementation Steps

### 1. Create keyword dictionaries (30min)

Expand from `TopicAnalyzer` with Vietnamese variations:

```csharp
namespace MessengerWebhook.Services.SubIntent;

public sealed class KeywordSubIntentDetector : ISubIntentClassifier
{
    private static readonly Dictionary<SubIntentCategory, HashSet<string>> Keywords = new()
    {
        [SubIntentCategory.ProductQuestion] = new()
        {
            // Features
            "công dụng", "tác dụng", "hiệu quả", "dùng để", "làm gì",
            // Ingredients
            "thành phần", "có gì", "chứa", "ingredient", "công thức",
            "có paraben", "có hóa chất", "tự nhiên",
            // Usage
            "cách dùng", "dùng như thế nào", "sử dụng", "thoa", "bôi", "apply",
            "dùng khi nào", "dùng buổi nào", "dùng sáng", "dùng tối"
        },
        
        [SubIntentCategory.PriceQuestion] = new()
        {
            "giá", "bao nhiêu", "tiền", "đắt", "rẻ", "giá cả", "chi phí",
            "giá bao nhiêu", "giá bn", "giá bnh", "bao nhiu", "bn tiền",
            "giảm giá", "sale", "khuyến mãi", "km", "voucher", "mã giảm"
        },
        
        [SubIntentCategory.ShippingQuestion] = new()
        {
            "giao", "ship", "vận chuyển", "nhận hàng", "giao hàng", "chuyển phát",
            "ship mất bao lâu", "bao lâu nhận", "khi nào nhận", "giao bao lâu",
            "phí ship", "freeship", "free ship", "miễn phí ship", "ship cod",
            "tracking", "mã vận đơn", "tra cứu đơn"
        },
        
        [SubIntentCategory.PolicyQuestion] = new()
        {
            "đổi trả", "hoàn tiền", "bảo hành", "chính sách", "policy",
            "đổi hàng", "trả hàng", "refund", "hoàn lại", "đền bù",
            "quà tặng", "tặng kèm", "gift", "bonus", "khuyến mãi kèm"
        },
        
        [SubIntentCategory.AvailabilityQuestion] = new()
        {
            "còn hàng", "còn không", "còn ko", "hết hàng", "out of stock",
            "có sẵn", "có hàng", "tồn kho", "availability", "in stock",
            "còn size", "còn màu", "còn mùi"
        },
        
        [SubIntentCategory.ComparisonQuestion] = new()
        {
            "so sánh", "khác gì", "khác nhau", "compare", "difference",
            "tốt hơn", "hơn", "vs", "hay hơn", "nên chọn",
            "giống nhau", "khác biệt", "phân biệt"
        }
    };
}
```

### 2. Implement Detect method (45min)

```csharp
public Task<SubIntentResult?> ClassifyAsync(
    string message,
    ConversationContext? conversationContext = null,
    CancellationToken cancellationToken = default)
{
    var result = Detect(message);
    return Task.FromResult(result);
}

private SubIntentResult? Detect(string message)
{
    if (string.IsNullOrWhiteSpace(message))
        return null;
    
    var normalized = message.ToLowerInvariant().Trim();
    var categoryScores = new Dictionary<SubIntentCategory, (int MatchCount, List<string> Matched)>();
    
    // Match keywords for each category
    foreach (var (category, keywords) in Keywords)
    {
        var matched = new List<string>();
        foreach (var keyword in keywords)
        {
            if (normalized.Contains(keyword))
            {
                matched.Add(keyword);
            }
        }
        
        if (matched.Count > 0)
        {
            categoryScores[category] = (matched.Count, matched);
        }
    }
    
    // No matches
    if (categoryScores.Count == 0)
        return null;
    
    // Find highest scoring category
    var best = categoryScores.OrderByDescending(kvp => kvp.Value.MatchCount).First();
    var confidence = CalculateConfidence(best.Value.MatchCount);
    
    return SubIntentResult.Create(
        category: best.Key,
        confidence: confidence,
        matchedKeywords: best.Value.Matched.ToArray(),
        explanation: $"Matched {best.Value.MatchCount} keywords",
        source: "keyword");
}

private static decimal CalculateConfidence(int matchCount)
{
    return matchCount switch
    {
        >= 3 => 0.95m,  // Very high confidence
        2 => 0.85m,     // High confidence
        1 => 0.65m,     // Medium confidence
        _ => 0.0m
    };
}
```

### 3. Handle edge cases (30min)

**Multi-intent messages:**
```csharp
// "giá bao nhiêu và ship mất bao lâu?"
// → Returns highest confidence (price: 2 matches vs shipping: 2 matches)
// → Tie-breaker: first in enum order
```

**Negation handling:**
```csharp
// "không đắt" → should NOT match PriceQuestion
// Simple solution: ignore for now (YAGNI)
// AI classifier will handle ambiguous cases
```

**Informal spelling:**
```csharp
// Already covered in keyword lists:
// "bao nhiêu" + "bao nhiu" + "bn" + "bnh"
```

### 4. Add unit tests (15min)

Create test stubs (full tests in Phase 7):

```csharp
// tests/MessengerWebhook.UnitTests/Services/SubIntent/KeywordSubIntentDetectorTests.cs
[Fact]
public async Task Detect_PriceQuestion_SingleKeyword()
{
    var detector = new KeywordSubIntentDetector();
    var result = await detector.ClassifyAsync("giá bao nhiêu?");
    
    Assert.NotNull(result);
    Assert.Equal(SubIntentCategory.PriceQuestion, result.Category);
    Assert.True(result.Confidence >= 0.6m);
}
```

## Todo List

- [ ] Create `KeywordSubIntentDetector.cs` class
- [ ] Define keyword dictionaries for 6 categories
- [ ] Implement `ClassifyAsync` method (delegates to sync `Detect`)
- [ ] Implement `Detect` method with keyword matching
- [ ] Implement `CalculateConfidence` based on match count
- [ ] Handle edge cases (multi-intent, empty message)
- [ ] Add XML documentation
- [ ] Create unit test file with basic tests
- [ ] Compile and verify no errors
- [ ] Benchmark performance (<10ms target)

## Success Criteria

- [ ] Detects all 6 sub-intent categories correctly
- [ ] Returns confidence scores: 0.65 (1 match), 0.85 (2 matches), 0.95 (3+ matches)
- [ ] Handles Vietnamese informal spelling variations
- [ ] Returns matched keywords for debugging
- [ ] Latency <10ms (synchronous, no async overhead)
- [ ] Thread-safe (stateless, immutable dictionaries)
- [ ] Unit tests pass for basic scenarios

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Keyword lists incomplete | High | Medium | Start with TopicAnalyzer keywords, extend in production |
| False positives (e.g., "không đắt") | Medium | Low | AI classifier handles ambiguous cases |
| Multi-intent messages ambiguous | Medium | Low | Return highest confidence, AI handles ties |
| Performance regression | Low | Low | Benchmark shows <10ms, no I/O |

## Security Considerations

- No PII stored (message passed as param)
- No external API calls (pure in-memory)
- No injection risk (simple string matching)

## Next Steps

**Blocks:** Phase 3 (Gemini AI Classifier)

**After completion:**
1. Benchmark performance (should be <10ms)
2. Review keyword coverage with Vietnamese speakers
3. Proceed to Phase 3: AI classifier for ambiguous cases
