# Phase 3: Conversation Context Analyzer

**Status:** pending  
**Priority:** high  
**Timeline:** Days 5-6  
**Dependencies:** Phase 1 (Emotion Detection), Phase 2 (Tone Matching)

## Context Links

- Plan Overview: `plan.md`
- Previous Phase: `phase-02-tone-matching-service.md`
- Emotion Detection: `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs`
- Tone Matching: `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs`
- Conversation History: `SalesStateHandlerBase.cs` (GetHistory/AddToHistory methods)

## Overview

Implement conversation context analyzer to extract patterns, detect topic shifts, identify buying signals, and track conversation quality from message history. Service analyzes 10-turn window for actionable insights to improve bot decision-making.

**Priority:** High - Enables context-aware responses  
**Current Status:** Not started  
**Estimated Effort:** 4-5 hours

## Key Insights

From completed phases:
- EmotionDetectionService provides emotion trends over time
- ToneMatchingService adapts tone based on customer context
- Conversation history stored in StateContext via GetHistory/AddToHistory
- ConversationMessage model: Role (user/model), Content, Timestamp

From existing codebase:
- History stored as List<ConversationMessage> in StateContext.Data["conversationHistory"]
- SalesStateHandlerBase tracks consultation rejections, product mentions
- Need to detect: repeat questions, topic shifts, buying signals, engagement drops

Vietnamese conversation patterns:
- Buying signals: "đặt", "mua", "chốt đơn", "lấy luôn", "gửi cho em"
- Hesitation: "suy nghĩ thêm", "để em xem", "chưa chắc", "hơi đắt"
- Topic shifts: sudden product change, question after answer, new concern
- Repeat questions: same intent within 3-5 turns

## Requirements

### Functional Requirements

1. **Pattern Detection**
   - Repeat questions (same intent within window)
   - Topic shifts (product change, concern switch)
   - Buying signals (purchase intent keywords)
   - Engagement patterns (response length, frequency)

2. **Context Analysis**
   - Conversation quality score (0-100)
   - Dominant topics (product, price, shipping, etc.)
   - Customer journey stage (browsing, considering, ready)
   - Conversation momentum (increasing/decreasing engagement)

3. **Actionable Insights**
   - Suggest next action (offer discount, escalate, close sale)
   - Flag concerns (price sensitivity, delivery anxiety)
   - Detect stalling patterns (needs nudge vs needs space)
   - Identify information gaps (missing product details, unclear policy)

4. **Performance Optimization**
   - Analyze only recent window (default 10 turns)
   - Cache analysis results per conversation
   - Incremental updates (don't reanalyze entire history)

### Non-Functional Requirements

- Performance: < 50ms for 10-turn analysis
- Accuracy: 85%+ pattern detection
- Memory: < 5MB cache per conversation
- Scalability: Handle 1000+ concurrent conversations

## Architecture

### Component Design

```
ConversationContextAnalyzer
├── Input: List<ConversationMessage>, EmotionScore[], ToneProfile[]
├── Processing:
│   ├── DetectPatterns (repeats, shifts, signals)
│   ├── AnalyzeTopics (extract dominant themes)
│   ├── CalculateQuality (engagement, coherence)
│   ├── DetermineJourneyStage (browsing → ready)
│   └── GenerateInsights (actionable recommendations)
└── Output: ConversationContext
```

### Data Flow

```
User Message 
→ EmotionDetectionService (emotion)
→ ToneMatchingService (tone)
→ ConversationContextAnalyzer (history + emotion + tone)
→ ConversationContext (patterns + insights)
→ SalesStateHandlerBase (use in decision-making)
→ Enhanced response
```

### New Files Structure

```
src/MessengerWebhook/Services/Conversation/
├── IConversationContextAnalyzer.cs          # Interface
├── ConversationContextAnalyzer.cs           # Main implementation
├── PatternDetector.cs                       # Pattern detection logic
├── TopicAnalyzer.cs                         # Topic extraction
├── Models/
│   ├── ConversationContext.cs               # Output model
│   ├── ConversationPattern.cs               # Pattern types
│   ├── ConversationTopic.cs                 # Topic model
│   ├── JourneyStage.cs                      # Enum: Browsing, Considering, Ready
│   ├── ConversationInsight.cs               # Actionable insight
│   └── ConversationQuality.cs               # Quality metrics
└── Configuration/
    └── ConversationAnalysisOptions.cs       # Configuration
```

## Related Code Files

### Files to Create

1. `src/MessengerWebhook/Services/Conversation/IConversationContextAnalyzer.cs`
2. `src/MessengerWebhook/Services/Conversation/ConversationContextAnalyzer.cs`
3. `src/MessengerWebhook/Services/Conversation/PatternDetector.cs`
4. `src/MessengerWebhook/Services/Conversation/TopicAnalyzer.cs`
5. `src/MessengerWebhook/Services/Conversation/Models/ConversationContext.cs`
6. `src/MessengerWebhook/Services/Conversation/Models/ConversationPattern.cs`
7. `src/MessengerWebhook/Services/Conversation/Models/ConversationTopic.cs`
8. `src/MessengerWebhook/Services/Conversation/Models/JourneyStage.cs`
9. `src/MessengerWebhook/Services/Conversation/Models/ConversationInsight.cs`
10. `src/MessengerWebhook/Services/Conversation/Models/ConversationQuality.cs`
11. `src/MessengerWebhook/Services/Conversation/Configuration/ConversationAnalysisOptions.cs`
12. `tests/MessengerWebhook.UnitTests/Services/Conversation/ConversationContextAnalyzerTests.cs`

### Files to Modify

1. `src/MessengerWebhook/Program.cs` - Register ConversationContextAnalyzer
2. `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - Integrate context analysis
3. `src/MessengerWebhook/appsettings.json` - Add conversation analysis config

### Files to Read (Dependencies)

- `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs` - Emotion trends
- `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs` - Tone consistency
- `src/MessengerWebhook/Services/AI/Models/ConversationMessage.cs` - Message model
- `src/MessengerWebhook/StateMachine/Models/StateContext.cs` - Context storage

## Implementation Steps

### Step 1: Create Models & Enums (1 hour)

**1.1 Create JourneyStage enum**
```csharp
public enum JourneyStage
{
    Browsing,       // Khách đang xem, hỏi thông tin
    Considering,    // Khách đang cân nhắc, so sánh
    Ready,          // Khách sẵn sàng mua
    Stalled,        // Khách dừng lại, cần nudge
    Abandoned       // Khách bỏ cuộc
}
```

**1.2 Create ConversationPattern model**
```csharp
public class ConversationPattern
{
    public PatternType Type { get; set; }
    public int Occurrences { get; set; }
    public List<int> TurnIndices { get; set; } = new();
    public double Confidence { get; set; }
    public string? Description { get; set; }
}

public enum PatternType
{
    RepeatQuestion,      // Hỏi lại câu cũ
    TopicShift,          // Đổi chủ đề đột ngột
    BuyingSignal,        // Tín hiệu mua hàng
    Hesitation,          // Do dự, chưa chắc
    PriceSensitivity,    // Nhạy cảm về giá
    EngagementDrop,      // Giảm tương tác
    InformationGap       // Thiếu thông tin
}
```

**1.3 Create ConversationTopic model**
```csharp
public class ConversationTopic
{
    public string Name { get; set; } = string.Empty;
    public int MentionCount { get; set; }
    public double Relevance { get; set; }  // 0-1
    public List<string> Keywords { get; set; } = new();
}
```

**1.4 Create ConversationQuality model**
```csharp
public class ConversationQuality
{
    public double Score { get; set; }  // 0-100
    public double Coherence { get; set; }  // Conversation flow
    public double Engagement { get; set; }  // Customer participation
    public double Momentum { get; set; }  // Increasing/decreasing
    public Dictionary<string, double> Metrics { get; set; } = new();
}
```

**1.5 Create ConversationInsight model**
```csharp
public class ConversationInsight
{
    public InsightType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? SuggestedAction { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum InsightType
{
    SuggestDiscount,     // Đề xuất giảm giá
    EscalateToHuman,     // Chuyển người
    CloseSale,           // Chốt đơn ngay
    ProvideMoreInfo,     // Cung cấp thêm thông tin
    GiveSpace,           // Cho khách thời gian
    AddressObjection     // Giải quyết lo ngại
}
```

**1.6 Create ConversationContext model (main output)**
```csharp
public class ConversationContext
{
    public JourneyStage CurrentStage { get; set; }
    public List<ConversationPattern> Patterns { get; set; } = new();
    public List<ConversationTopic> Topics { get; set; } = new();
    public ConversationQuality Quality { get; set; } = new();
    public List<ConversationInsight> Insights { get; set; } = new();
    public int TurnCount { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### Step 2: Create Configuration (15 min)

**2.1 Create ConversationAnalysisOptions.cs**
```csharp
public class ConversationAnalysisOptions
{
    public int AnalysisWindowSize { get; set; } = 10;
    public bool EnablePatternDetection { get; set; } = true;
    public bool EnableTopicAnalysis { get; set; } = true;
    public bool EnableInsightGeneration { get; set; } = true;
    public double BuyingSignalThreshold { get; set; } = 0.7;
    public double RepeatQuestionThreshold { get; set; } = 0.8;
    public int RepeatQuestionWindow { get; set; } = 5;
    public bool EnableCaching { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 10;
}
```

**2.2 Update appsettings.json**
```json
"ConversationAnalysis": {
    "AnalysisWindowSize": 10,
    "EnablePatternDetection": true,
    "EnableTopicAnalysis": true,
    "EnableInsightGeneration": true,
    "BuyingSignalThreshold": 0.7,
    "RepeatQuestionThreshold": 0.8,
    "RepeatQuestionWindow": 5,
    "EnableCaching": true,
    "CacheDurationMinutes": 10
}
```

### Step 3: Implement PatternDetector (1 hour)

**3.1 Create PatternDetector.cs**

Key methods:
- `DetectRepeatQuestions(history)` - Find similar questions within window
- `DetectTopicShifts(history)` - Identify sudden topic changes
- `DetectBuyingSignals(history)` - Find purchase intent keywords
- `DetectHesitation(history)` - Identify uncertainty patterns
- `DetectEngagementDrop(history)` - Track response length/frequency

Vietnamese keyword sets:
```csharp
private static readonly HashSet<string> BuyingSignals = new()
{
    "đặt", "mua", "chốt", "lấy", "gửi", "order", "đặt hàng", "mua luôn"
};

private static readonly HashSet<string> HesitationSignals = new()
{
    "suy nghĩ", "xem thêm", "chưa chắc", "để em", "hơi đắt", "đắt quá"
};

private static readonly HashSet<string> PriceKeywords = new()
{
    "giá", "bao nhiêu", "tiền", "đắt", "rẻ", "giảm", "khuyến mãi"
};
```

Similarity detection for repeat questions:
```csharp
private double CalculateSimilarity(string msg1, string msg2)
{
    // Simple word overlap ratio
    var words1 = msg1.ToLower().Split(' ').ToHashSet();
    var words2 = msg2.ToLower().Split(' ').ToHashSet();
    var intersection = words1.Intersect(words2).Count();
    var union = words1.Union(words2).Count();
    return union > 0 ? (double)intersection / union : 0;
}
```

### Step 4: Implement TopicAnalyzer (45 min)

**4.1 Create TopicAnalyzer.cs**

Key methods:
- `ExtractTopics(history)` - Identify dominant topics
- `CalculateRelevance(topic, history)` - Score topic importance
- `TrackTopicShifts(history)` - Detect topic transitions

Topic categories:
```csharp
private static readonly Dictionary<string, HashSet<string>> TopicKeywords = new()
{
    ["product"] = new() { "sản phẩm", "mỹ phẩm", "kem", "serum", "toner" },
    ["price"] = new() { "giá", "bao nhiêu", "tiền", "đắt", "rẻ" },
    ["shipping"] = new() { "giao", "ship", "vận chuyển", "nhận hàng" },
    ["quality"] = new() { "chất lượng", "tốt", "xịn", "fake", "thật" },
    ["usage"] = new() { "dùng", "sử dụng", "cách dùng", "thoa" },
    ["ingredients"] = new() { "thành phần", "có gì", "chứa" }
};
```

### Step 5: Implement Main Service (1.5 hours)

**5.1 Create IConversationContextAnalyzer.cs**
```csharp
public interface IConversationContextAnalyzer
{
    Task<ConversationContext> AnalyzeAsync(
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);
    
    Task<ConversationContext> AnalyzeWithEmotionAsync(
        List<ConversationMessage> history,
        List<EmotionScore> emotionHistory,
        CancellationToken cancellationToken = default);
}
```

**5.2 Create ConversationContextAnalyzer.cs**

Constructor dependencies:
- PatternDetector
- TopicAnalyzer
- IMemoryCache
- ILogger
- IOptions<ConversationAnalysisOptions>

Main analysis flow:
```csharp
public async Task<ConversationContext> AnalyzeAsync(
    List<ConversationMessage> history,
    CancellationToken cancellationToken = default)
{
    // 1. Limit to analysis window
    var recentHistory = history
        .TakeLast(_options.AnalysisWindowSize)
        .ToList();
    
    // 2. Detect patterns
    var patterns = _options.EnablePatternDetection
        ? _patternDetector.DetectPatterns(recentHistory)
        : new List<ConversationPattern>();
    
    // 3. Extract topics
    var topics = _options.EnableTopicAnalysis
        ? _topicAnalyzer.ExtractTopics(recentHistory)
        : new List<ConversationTopic>();
    
    // 4. Calculate quality
    var quality = CalculateQuality(recentHistory, patterns);
    
    // 5. Determine journey stage
    var stage = DetermineJourneyStage(patterns, topics, quality);
    
    // 6. Generate insights
    var insights = _options.EnableInsightGeneration
        ? GenerateInsights(stage, patterns, topics, quality)
        : new List<ConversationInsight>();
    
    return new ConversationContext
    {
        CurrentStage = stage,
        Patterns = patterns,
        Topics = topics,
        Quality = quality,
        Insights = insights,
        TurnCount = recentHistory.Count,
        AnalyzedAt = DateTime.UtcNow
    };
}
```

**5.3 Implement DetermineJourneyStage**
```csharp
private JourneyStage DetermineJourneyStage(
    List<ConversationPattern> patterns,
    List<ConversationTopic> topics,
    ConversationQuality quality)
{
    // Check for buying signals
    var hasBuyingSignal = patterns.Any(p => 
        p.Type == PatternType.BuyingSignal && 
        p.Confidence >= _options.BuyingSignalThreshold);
    
    if (hasBuyingSignal)
        return JourneyStage.Ready;
    
    // Check for hesitation
    var hasHesitation = patterns.Any(p => 
        p.Type == PatternType.Hesitation);
    
    if (hasHesitation)
        return JourneyStage.Considering;
    
    // Check for engagement drop
    var hasEngagementDrop = patterns.Any(p => 
        p.Type == PatternType.EngagementDrop);
    
    if (hasEngagementDrop && quality.Momentum < 0.3)
        return JourneyStage.Stalled;
    
    // Default to browsing
    return JourneyStage.Browsing;
}
```

**5.4 Implement GenerateInsights**
```csharp
private List<ConversationInsight> GenerateInsights(
    JourneyStage stage,
    List<ConversationPattern> patterns,
    List<ConversationTopic> topics,
    ConversationQuality quality)
{
    var insights = new List<ConversationInsight>();
    
    // Stage-based insights
    if (stage == JourneyStage.Ready)
    {
        insights.Add(new ConversationInsight
        {
            Type = InsightType.CloseSale,
            Message = "Customer showing strong buying signals - ready to close",
            Confidence = 0.9,
            SuggestedAction = "Offer to finalize order with contact info"
        });
    }
    
    // Pattern-based insights
    var priceSensitivity = patterns.FirstOrDefault(p => 
        p.Type == PatternType.PriceSensitivity);
    
    if (priceSensitivity != null && priceSensitivity.Confidence > 0.7)
    {
        insights.Add(new ConversationInsight
        {
            Type = InsightType.SuggestDiscount,
            Message = "Customer price-sensitive - consider offering discount",
            Confidence = priceSensitivity.Confidence,
            SuggestedAction = "Mention current promotions or bundle deals"
        });
    }
    
    // Quality-based insights
    if (quality.Engagement < 0.3)
    {
        insights.Add(new ConversationInsight
        {
            Type = InsightType.GiveSpace,
            Message = "Low engagement - customer may need time to think",
            Confidence = 0.8,
            SuggestedAction = "Offer to follow up later, don't push"
        });
    }
    
    return insights;
}
```

### Step 6: Register Service in DI (15 min)

**6.1 Update Program.cs**
```csharp
// Add after ToneMatchingService registration
builder.Services.Configure<ConversationAnalysisOptions>(
    builder.Configuration.GetSection("ConversationAnalysis"));
builder.Services.AddSingleton<PatternDetector>();
builder.Services.AddSingleton<TopicAnalyzer>();
builder.Services.AddSingleton<IConversationContextAnalyzer, ConversationContextAnalyzer>();
```

### Step 7: Integration with SalesStateHandlerBase (30 min)

**7.1 Inject IConversationContextAnalyzer**
- Add constructor parameter
- Store as protected field

**7.2 Update HandleSalesConversationAsync**
```csharp
// After emotion and tone detection
var history = GetHistory(ctx);

// Analyze conversation context
var conversationContext = await _conversationContextAnalyzer
    .AnalyzeAsync(history, cancellationToken);

// Store in context for decision-making
ctx.SetData("conversationContext", conversationContext);

// Use insights in prompt
if (conversationContext.Insights.Any())
{
    var insightText = string.Join("\n", 
        conversationContext.Insights.Select(i => 
            $"- {i.Message} (Action: {i.SuggestedAction})"));
    
    // Add to system prompt or use in decision logic
}

// Log for monitoring
Logger.LogInformation(
    "Conversation analysis - Stage: {Stage}, Quality: {Quality}, Patterns: {Patterns}",
    conversationContext.CurrentStage,
    conversationContext.Quality.Score,
    conversationContext.Patterns.Count);
```

### Step 8: Unit Tests (1 hour)

**8.1 Create ConversationContextAnalyzerTests.cs**

Test cases:
1. **Buying signal detection** - "Đặt luôn nha" → Ready stage
2. **Repeat question detection** - Same question twice → RepeatQuestion pattern
3. **Topic shift detection** - Product A → Product B → TopicShift pattern
4. **Hesitation detection** - "Để em suy nghĩ" → Considering stage
5. **Price sensitivity** - Multiple price questions → PriceSensitivity pattern
6. **Engagement drop** - Short responses → EngagementDrop pattern
7. **Journey stage progression** - Browsing → Considering → Ready
8. **Insight generation** - Ready stage → CloseSale insight
9. **Quality calculation** - High engagement → high quality score
10. **Caching works** - Same history returns cached result

**8.2 Test structure example**
```csharp
[Fact]
public async Task AnalyzeAsync_BuyingSignalDetected_ReturnsReadyStage()
{
    // Arrange
    var history = new List<ConversationMessage>
    {
        new() { Role = "user", Content = "Cho em xem sản phẩm này" },
        new() { Role = "model", Content = "Dạ đây ạ chị" },
        new() { Role = "user", Content = "Đặt luôn nha" }
    };
    
    // Act
    var context = await _analyzer.AnalyzeAsync(history);
    
    // Assert
    Assert.Equal(JourneyStage.Ready, context.CurrentStage);
    Assert.Contains(context.Patterns, p => 
        p.Type == PatternType.BuyingSignal);
    Assert.Contains(context.Insights, i => 
        i.Type == InsightType.CloseSale);
}
```

## Todo List

- [ ] Create JourneyStage enum
- [ ] Create ConversationPattern model
- [ ] Create ConversationTopic model
- [ ] Create ConversationQuality model
- [ ] Create ConversationInsight model
- [ ] Create ConversationContext model
- [ ] Create ConversationAnalysisOptions configuration
- [ ] Update appsettings.json
- [ ] Implement PatternDetector class
- [ ] Implement TopicAnalyzer class
- [ ] Create IConversationContextAnalyzer interface
- [ ] Implement ConversationContextAnalyzer main logic
- [ ] Implement DetermineJourneyStage method
- [ ] Implement GenerateInsights method
- [ ] Implement CalculateQuality method
- [ ] Add caching logic
- [ ] Register services in Program.cs DI
- [ ] Integrate with SalesStateHandlerBase
- [ ] Write unit tests (10+ test cases)
- [ ] Run tests and verify 85%+ coverage
- [ ] Performance testing (< 50ms target)
- [ ] Manual testing with real conversations
- [ ] Code review and documentation

## Success Criteria

### Functional
- ✅ Pattern detection working for all pattern types
- ✅ Topic extraction accurate for Vietnamese conversations
- ✅ Journey stage determination logical and consistent
- ✅ Insights actionable and relevant
- ✅ Integration with SalesStateHandlerBase complete

### Performance
- ✅ Response time < 50ms for 10-turn history (p95)
- ✅ Caching working correctly
- ✅ Memory usage < 5MB per conversation
- ✅ No performance regression in overall response time

### Quality
- ✅ Unit test coverage ≥ 85%
- ✅ All tests passing
- ✅ Context analysis accuracy: 85%+ (manual validation)
- ✅ Pattern detection accuracy: 85%+ (manual validation)
- ✅ Code review approved

## Risk Assessment

### High Risk
1. **Vietnamese Language Complexity**
   - Risk: Keyword matching misses slang, regional variations
   - Likelihood: Medium | Impact: High
   - Mitigation: Comprehensive keyword sets, fuzzy matching, expand based on production data
   - Contingency: ML-based intent detection in future phase

### Medium Risk
2. **False Positive Patterns**
   - Risk: Detecting patterns that don't exist (e.g., false buying signals)
   - Likelihood: Medium | Impact: Medium
   - Mitigation: Confidence thresholds, require multiple signals, context validation
   - Contingency: Tune thresholds based on A/B testing

3. **Performance Impact**
   - Risk: Analysis adds latency to response time
   - Likelihood: Low | Impact: Medium
   - Mitigation: Limit window size, caching, async processing, optimize hot paths
   - Contingency: Disable analysis for low-priority conversations

### Low Risk
4. **Cache Invalidation**
   - Risk: Stale cached results after new messages
   - Likelihood: Low | Impact: Low
   - Mitigation: Cache key includes turn count, short TTL (10 min)
   - Contingency: Disable caching if issues arise

## Security Considerations

- Input validation: Sanitize message content before analysis
- Logging: Don't log sensitive customer data (phone, address, payment info)
- Configuration: Validate ConversationAnalysisOptions on startup
- Caching: Use memory cache (not distributed) to avoid data leaks
- Rate limiting: Prevent abuse of analysis endpoint

## Performance Considerations

- **Window size**: Limit to 10 turns (configurable) to control processing time
- **Caching strategy**: Cache by (sessionId + turnCount) for 10 minutes
- **Incremental analysis**: Only analyze new messages, not entire history
- **Async operations**: All methods async to avoid blocking
- **Memory management**: Clear old cache entries, limit cache size
- **Hot path optimization**: Pattern detection is O(n) where n = window size

## Integration Points

### Upstream Dependencies
- **EmotionDetectionService** (Phase 1): Emotion trends for quality calculation
- **ToneMatchingService** (Phase 2): Tone consistency validation
- **SalesStateHandlerBase** (existing): Conversation history via GetHistory()
- **StateContext** (existing): Store analysis results

### Downstream Consumers
- **SalesStateHandlerBase** (existing): Use insights in decision-making
- **Phase 4: Small Talk Service**: Use context to determine when to engage
- **Phase 5: Response Validator**: Validate response matches context
- **Future: Analytics Dashboard**: Display conversation metrics

## Testing Strategy

### Unit Tests (85%+ coverage)
- All public methods tested
- Edge cases: empty history, single message, max window size
- Pattern detection for each pattern type
- Topic extraction for each topic category
- Journey stage transitions
- Insight generation logic

### Integration Tests (Phase 6)
- End-to-end flow: Message → Context → Insights → Decision
- Verify insights used in SalesStateHandlerBase
- Test with real conversation scenarios
- Performance testing with large histories

### Manual Testing
- Test with real Vietnamese conversations
- Verify pattern detection accuracy
- Validate insights are actionable
- Test edge cases (very short/long conversations)

## Migration & Backwards Compatibility

**No breaking changes:**
- New service, no existing code depends on it
- SalesStateHandlerBase modification is additive (new context data)
- Existing conversation flow unchanged

**Rollback plan:**
- Remove ConversationContextAnalyzer from DI registration
- Remove context analysis code from SalesStateHandlerBase
- System falls back to existing behavior (no context analysis)

**Feature flag:**
- Add `EnableConversationAnalysis` flag in appsettings.json
- Easy to disable if issues arise in production

## Next Steps

After Phase 3 completion:
1. Collect production metrics: pattern accuracy, insight relevance
2. Tune thresholds based on false positive/negative rates
3. Move to Phase 4: Small Talk & Natural Flow
4. Expand keyword sets based on production conversations
5. Consider ML-based intent detection for future enhancement

## Unresolved Questions

1. **Similarity threshold for repeat questions**: What similarity score (0-1) should trigger repeat detection?
   - **Recommendation**: Start with 0.8, tune based on false positives

2. **Topic shift sensitivity**: How many messages between topics before it's a "shift" vs natural flow?
   - **Recommendation**: 2-3 messages, configurable

3. **Insight priority**: When multiple insights conflict (e.g., CloseSale vs GiveSpace), which takes precedence?
   - **Recommendation**: Use confidence scores, highest confidence wins

4. **Cache invalidation strategy**: Should cache clear on emotion escalation or tone shift?
   - **Recommendation**: No, cache is per-turn, natural invalidation on new message

5. **Integration with existing consultation rejection tracking**: Should ConversationContext replace or complement existing logic?
   - **Recommendation**: Complement, use both signals for better decision-making
