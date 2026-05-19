# Phase 4: Small Talk & Natural Flow

**Status:** pending  
**Priority:** high  
**Timeline:** Day 6  
**Dependencies:** Phase 1 (Emotion Detection), Phase 2 (Tone Matching), Phase 3 (Conversation Context)

## Context Links

- Plan Overview: `plan.md`
- Previous Phase: `phase-03-conversation-context-analyzer.md`
- Emotion Detection: `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs`
- Tone Matching: `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs`
- Context Analyzer: `src/MessengerWebhook/Services/Conversation/ConversationContextAnalyzer.cs`

## Overview

Implement small talk detection and natural conversation flow to handle casual greetings ("hi sốp", "alo shop") without forcing business conversation. Service detects small talk intent, generates appropriate casual responses, and gracefully transitions to business when customer shows buying signals.

**Priority:** High - Core naturalness feature  
**Current Status:** Not started  
**Estimated Effort:** 3-4 hours

## Key Insights

From completed phases:
- EmotionDetectionService detects Positive/Excited emotions for casual messages
- ToneMatchingService provides Casual tone level for friendly interactions
- ConversationContextAnalyzer tracks JourneyStage (Browsing → Considering → Ready)
- Existing greeting detection in CompleteStateHandler (lines 102-108)

From existing codebase:
- GreetingStateHandler transitions to MainMenu/SkinConsultation/OrderTracking
- SalesStateHandlerBase has tone mirroring instructions (line 675, 695)
- Current issue: Returning customers get full catalog intro even for casual "hi"

Vietnamese small talk patterns:
- Greetings: "hi", "hello", "chào", "alo", "hi sốp", "chào shop"
- Check-ins: "shop ơi", "có ai không", "còn bán không"
- Casual questions: "shop mở cửa không", "hôm nay có gì mới"
- Weather/time: "trời đẹp nhỉ", "buổi sáng vui vẻ"

## Requirements

### Functional Requirements

1. **Small Talk Detection**
   - Detect casual greetings vs business intent
   - Identify check-in messages ("có ai không", "shop ơi")
   - Recognize social pleasantries (weather, time of day)
   - Distinguish from product inquiries

2. **Natural Response Generation**
   - Generate casual responses matching customer tone
   - Avoid forcing business conversation immediately
   - Use context-aware greetings (time of day, returning customer)
   - Keep responses short (1-2 sentences)

3. **Graceful Transition**
   - Detect buying signals in follow-up messages
   - Transition from small talk to business naturally
   - Offer help without being pushy ("Có gì em giúp được không?")
   - Track conversation stage (small talk → browsing → considering)

4. **Context Awareness**
   - Use VIP profile for personalized greetings
   - Reference previous orders for returning customers
   - Adapt to time of day (morning/afternoon/evening)
   - Consider emotion and tone from previous phases

### Non-Functional Requirements

- Performance: < 100ms small talk detection
- Accuracy: 90%+ small talk vs business intent classification
- Response quality: 85%+ naturalness rating
- No forced transitions: Let customer lead conversation pace

## Architecture

### Component Design

```
SmallTalkService
├── Input: Message, ConversationContext, ToneProfile, EmotionScore
├── Processing:
│   ├── DetectSmallTalkIntent (greeting, check-in, pleasantry)
│   ├── GenerateResponse (casual, context-aware)
│   ├── DetermineTransitionReadiness (small talk → business)
│   └── BuildSmallTalkContext (for prompt injection)
└── Output: SmallTalkResponse
```

### Data Flow

```
User Message ("hi sốp")
→ EmotionDetectionService (Positive/Excited)
→ ToneMatchingService (Casual tone)
→ ConversationContextAnalyzer (Browsing stage)
→ SmallTalkService (detect intent + generate response)
→ SmallTalkResponse (casual greeting + soft offer)
→ SalesStateHandlerBase (inject into prompt or use directly)
→ Response: "Chào bạn! Có gì em giúp được không? 😊"
```

### New Files Structure

```
src/MessengerWebhook/Services/SmallTalk/
├── ISmallTalkService.cs                     # Interface
├── SmallTalkService.cs                      # Main implementation
├── SmallTalkDetector.cs                     # Intent detection
├── Models/
│   ├── SmallTalkResponse.cs                 # Output model
│   ├── SmallTalkIntent.cs                   # Enum: Greeting, CheckIn, etc.
│   ├── TransitionReadiness.cs               # Enum: StayInSmallTalk, ReadyForBusiness
│   └── SmallTalkContext.cs                  # Context for response generation
└── Configuration/
    └── SmallTalkOptions.cs                  # Configuration
```

## Related Code Files

### Files to Create

1. `src/MessengerWebhook/Services/SmallTalk/ISmallTalkService.cs`
2. `src/MessengerWebhook/Services/SmallTalk/SmallTalkService.cs`
3. `src/MessengerWebhook/Services/SmallTalk/SmallTalkDetector.cs`
4. `src/MessengerWebhook/Services/SmallTalk/Models/SmallTalkResponse.cs`
5. `src/MessengerWebhook/Services/SmallTalk/Models/SmallTalkIntent.cs`
6. `src/MessengerWebhook/Services/SmallTalk/Models/TransitionReadiness.cs`
7. `src/MessengerWebhook/Services/SmallTalk/Models/SmallTalkContext.cs`
8. `src/MessengerWebhook/Services/SmallTalk/Configuration/SmallTalkOptions.cs`
9. `tests/MessengerWebhook.UnitTests/Services/SmallTalk/SmallTalkServiceTests.cs`

### Files to Modify

1. `src/MessengerWebhook/Program.cs` - Register SmallTalkService
2. `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - Integrate small talk detection
3. `src/MessengerWebhook/StateMachine/Handlers/GreetingStateHandler.cs` - Use SmallTalkService
4. `src/MessengerWebhook/appsettings.json` - Add small talk config

### Files to Read (Dependencies)

- `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs` - Emotion input
- `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs` - Tone profile
- `src/MessengerWebhook/Services/Conversation/ConversationContextAnalyzer.cs` - Context
- `src/MessengerWebhook/StateMachine/Handlers/CompleteStateHandler.cs` - Existing greeting logic

## Implementation Steps

### Step 1: Create Models & Enums (30 min)

**1.1 Create SmallTalkIntent enum**
```csharp
public enum SmallTalkIntent
{
    None,              // Not small talk, business intent
    Greeting,          // "hi", "hello", "chào"
    CheckIn,           // "có ai không", "shop ơi"
    Pleasantry,        // "trời đẹp", "buổi sáng vui vẻ"
    Acknowledgment     // "ok", "oke", "được"
}
```

**1.2 Create TransitionReadiness enum**
```csharp
public enum TransitionReadiness
{
    StayInSmallTalk,      // Continue casual conversation
    SoftOffer,            // Offer help gently ("Có gì em giúp không?")
    ReadyForBusiness      // Customer showing buying signals
}
```

**1.3 Create SmallTalkResponse model**
```csharp
public class SmallTalkResponse
{
    public SmallTalkIntent Intent { get; set; }
    public bool IsSmallTalk { get; set; }
    public string? SuggestedResponse { get; set; }
    public TransitionReadiness TransitionReadiness { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

**1.4 Create SmallTalkContext model**
```csharp
public class SmallTalkContext
{
    public string Message { get; set; } = string.Empty;
    public EmotionScore Emotion { get; set; } = null!;
    public ToneProfile ToneProfile { get; set; } = null!;
    public ConversationContext ConversationContext { get; set; } = null!;
    public VipProfile VipProfile { get; set; } = null!;
    public bool IsReturningCustomer { get; set; }
    public int ConversationTurnCount { get; set; }
    public TimeOfDay TimeOfDay { get; set; }
}

public enum TimeOfDay
{
    Morning,    // 5am-12pm
    Afternoon,  // 12pm-6pm
    Evening     // 6pm-5am
}
```

### Step 2: Create Configuration (15 min)

**2.1 Create SmallTalkOptions.cs**
```csharp
public class SmallTalkOptions
{
    public bool EnableSmallTalkDetection { get; set; } = true;
    public bool EnableContextAwareGreetings { get; set; } = true;
    public double SmallTalkConfidenceThreshold { get; set; } = 0.7;
    public int MaxSmallTalkTurns { get; set; } = 3;
    public bool EnableSoftTransitions { get; set; } = true;
}
```

**2.2 Update appsettings.json**
```json
"SmallTalk": {
    "EnableSmallTalkDetection": true,
    "EnableContextAwareGreetings": true,
    "SmallTalkConfidenceThreshold": 0.7,
    "MaxSmallTalkTurns": 3,
    "EnableSoftTransitions": true
}
```

### Step 3: Implement SmallTalkDetector (1 hour)

**3.1 Create SmallTalkDetector.cs**

Key methods:
- `DetectIntent(message)` - Classify message intent
- `CalculateConfidence(message, intent)` - Confidence score
- `IsBusinessIntent(message)` - Check for product/order keywords

Vietnamese keyword sets:
```csharp
private static readonly HashSet<string> GreetingKeywords = new()
{
    "hi", "hello", "chào", "alo", "alô", "xin chào", "chào shop", 
    "hi shop", "hi sốp", "alo shop", "chào bạn"
};

private static readonly HashSet<string> CheckInKeywords = new()
{
    "có ai", "có người", "shop ơi", "có shop", "có bạn", 
    "còn bán", "còn hoạt động", "mở cửa"
};

private static readonly HashSet<string> PleasantryKeywords = new()
{
    "trời đẹp", "buổi sáng", "chúc", "cảm ơn", "thanks", "thank you"
};

private static readonly HashSet<string> BusinessKeywords = new()
{
    "sản phẩm", "mua", "đặt", "giá", "bao nhiêu", "ship", 
    "giao hàng", "order", "đơn hàng", "tư vấn", "xem"
};
```

Detection logic:
```csharp
public SmallTalkIntent DetectIntent(string message)
{
    var normalized = message.ToLower().Trim();
    
    // Check for business keywords first
    if (BusinessKeywords.Any(k => normalized.Contains(k)))
        return SmallTalkIntent.None;
    
    // Check greeting patterns
    if (GreetingKeywords.Any(k => normalized.StartsWith(k) || normalized == k))
        return SmallTalkIntent.Greeting;
    
    // Check check-in patterns
    if (CheckInKeywords.Any(k => normalized.Contains(k)))
        return SmallTalkIntent.CheckIn;
    
    // Check pleasantries
    if (PleasantryKeywords.Any(k => normalized.Contains(k)))
        return SmallTalkIntent.Pleasantry;
    
    // Short acknowledgments
    if (normalized.Length <= 5 && 
        new[] { "ok", "oke", "được", "uhm", "uh" }.Contains(normalized))
        return SmallTalkIntent.Acknowledgment;
    
    return SmallTalkIntent.None;
}
```

### Step 4: Implement Main Service (1.5 hours)

**4.1 Create ISmallTalkService.cs**
```csharp
public interface ISmallTalkService
{
    Task<SmallTalkResponse> AnalyzeAsync(
        SmallTalkContext context,
        CancellationToken cancellationToken = default);
    
    Task<SmallTalkResponse> AnalyzeAsync(
        string message,
        EmotionScore emotion,
        ToneProfile toneProfile,
        ConversationContext conversationContext,
        VipProfile vipProfile,
        bool isReturningCustomer,
        int conversationTurnCount,
        CancellationToken cancellationToken = default);
}
```

**4.2 Create SmallTalkService.cs**

Constructor dependencies:
- SmallTalkDetector
- ILogger
- IOptions<SmallTalkOptions>

Main analysis flow:
```csharp
public async Task<SmallTalkResponse> AnalyzeAsync(
    SmallTalkContext context,
    CancellationToken cancellationToken = default)
{
    // Detect intent
    var intent = _detector.DetectIntent(context.Message);
    var confidence = _detector.CalculateConfidence(context.Message, intent);
    
    // Not small talk if confidence too low or business intent
    if (intent == SmallTalkIntent.None || 
        confidence < _options.SmallTalkConfidenceThreshold)
    {
        return new SmallTalkResponse
        {
            Intent = SmallTalkIntent.None,
            IsSmallTalk = false,
            TransitionReadiness = TransitionReadiness.ReadyForBusiness,
            Confidence = confidence
        };
    }
    
    // Determine transition readiness
    var transitionReadiness = DetermineTransitionReadiness(
        context.ConversationContext,
        context.ConversationTurnCount);
    
    // Generate suggested response
    var suggestedResponse = GenerateResponse(
        intent,
        context,
        transitionReadiness);
    
    return new SmallTalkResponse
    {
        Intent = intent,
        IsSmallTalk = true,
        SuggestedResponse = suggestedResponse,
        TransitionReadiness = transitionReadiness,
        Confidence = confidence,
        Metadata = new Dictionary<string, object>
        {
            ["timeOfDay"] = context.TimeOfDay.ToString(),
            ["isReturning"] = context.IsReturningCustomer,
            ["turnCount"] = context.ConversationTurnCount
        }
    };
}
```

**4.3 Implement DetermineTransitionReadiness**
```csharp
private TransitionReadiness DetermineTransitionReadiness(
    ConversationContext conversationContext,
    int turnCount)
{
    // Check for buying signals in context
    var hasBuyingSignal = conversationContext.Patterns
        .Any(p => p.Type == PatternType.BuyingSignal);
    
    if (hasBuyingSignal || conversationContext.CurrentStage == JourneyStage.Ready)
        return TransitionReadiness.ReadyForBusiness;
    
    // After max small talk turns, offer help
    if (turnCount >= _options.MaxSmallTalkTurns)
        return TransitionReadiness.SoftOffer;
    
    // Stay in small talk for first few turns
    return TransitionReadiness.StayInSmallTalk;
}
```

**4.4 Implement GenerateResponse**
```csharp
private string GenerateResponse(
    SmallTalkIntent intent,
    SmallTalkContext context,
    TransitionReadiness transitionReadiness)
{
    var responses = new List<string>();
    
    // Generate base response based on intent
    switch (intent)
    {
        case SmallTalkIntent.Greeting:
            responses.Add(GenerateGreeting(context));
            break;
        
        case SmallTalkIntent.CheckIn:
            responses.Add("Dạ em đây ạ!");
            break;
        
        case SmallTalkIntent.Pleasantry:
            responses.Add("Dạ cảm ơn bạn! 😊");
            break;
        
        case SmallTalkIntent.Acknowledgment:
            responses.Add("Dạ vâng ạ!");
            break;
    }
    
    // Add transition based on readiness
    if (transitionReadiness == TransitionReadiness.SoftOffer)
    {
        responses.Add("Có gì em giúp được không ạ?");
    }
    else if (transitionReadiness == TransitionReadiness.ReadyForBusiness)
    {
        responses.Add("Em có thể tư vấn sản phẩm cho bạn nha!");
    }
    
    return string.Join(" ", responses);
}

private string GenerateGreeting(SmallTalkContext context)
{
    // Time-aware greeting
    var timeGreeting = context.TimeOfDay switch
    {
        TimeOfDay.Morning => "Chào buổi sáng",
        TimeOfDay.Afternoon => "Chào buổi chiều",
        TimeOfDay.Evening => "Chào buổi tối",
        _ => "Chào"
    };
    
    // VIP/returning customer personalization
    if (context.VipProfile.Tier == VipTier.Vip)
        return $"{timeGreeting} chị! Em rất vui được phục vụ chị ạ! 😊";
    
    if (context.IsReturningCustomer)
        return $"{timeGreeting} bạn! Vui được gặp lại bạn nha! 😊";
    
    // Casual tone for excited customers
    if (context.Emotion.PrimaryEmotion == EmotionType.Excited)
        return $"Alo bạn! 😊";
    
    // Default friendly greeting
    return $"{timeGreeting} bạn! 😊";
}
```

### Step 5: Register Service in DI (15 min)

**5.1 Update Program.cs**
```csharp
// Add after ConversationContextAnalyzer registration
builder.Services.Configure<SmallTalkOptions>(
    builder.Configuration.GetSection("SmallTalk"));
builder.Services.AddSingleton<SmallTalkDetector>();
builder.Services.AddSingleton<ISmallTalkService, SmallTalkService>();
```

### Step 6: Integration with SalesStateHandlerBase (30 min)

**6.1 Inject ISmallTalkService**
- Add constructor parameter
- Store as protected field

**6.2 Update HandleSalesConversationAsync**
```csharp
// After emotion, tone, and context analysis
var emotion = await EmotionDetectionService
    .DetectEmotionWithContextAsync(message, history, cancellationToken);

var toneProfile = await ToneMatchingService
    .GenerateToneProfileAsync(emotion, vipProfile, customer, 
        history.Count, cancellationToken);

var conversationContext = await ConversationContextAnalyzer
    .AnalyzeAsync(history, cancellationToken);

// Analyze for small talk
var timeOfDay = GetTimeOfDay();
var smallTalkResponse = await SmallTalkService.AnalyzeAsync(
    message,
    emotion,
    toneProfile,
    conversationContext,
    vipProfile,
    isReturningCustomer: ctx.GetData<bool?>("isReturningCustomer") == true,
    conversationTurnCount: history.Count,
    cancellationToken);

// Store in context
ctx.SetData("smallTalkResponse", smallTalkResponse);

// If small talk detected, use suggested response or inject into prompt
if (smallTalkResponse.IsSmallTalk)
{
    Logger.LogInformation(
        "Small talk detected: {Intent} (confidence: {Confidence:F2})",
        smallTalkResponse.Intent,
        smallTalkResponse.Confidence);
    
    // For pure greetings with no business intent, return suggested response
    if (smallTalkResponse.TransitionReadiness == TransitionReadiness.StayInSmallTalk &&
        history.Count <= 2)
    {
        AddToHistory(ctx, "model", smallTalkResponse.SuggestedResponse!);
        return smallTalkResponse.SuggestedResponse!;
    }
    
    // Otherwise, inject small talk context into prompt
    var smallTalkInstruction = $@"
## Small Talk Context
Customer message is small talk ({smallTalkResponse.Intent}).
Transition readiness: {smallTalkResponse.TransitionReadiness}
Suggested approach: {smallTalkResponse.SuggestedResponse}

Keep response casual and natural. Don't force business conversation.
";
    // Add to system prompt
}
```

**6.3 Add GetTimeOfDay helper**
```csharp
private TimeOfDay GetTimeOfDay()
{
    var hour = DateTime.Now.Hour;
    if (hour >= 5 && hour < 12) return TimeOfDay.Morning;
    if (hour >= 12 && hour < 18) return TimeOfDay.Afternoon;
    return TimeOfDay.Evening;
}
```

### Step 7: Unit Tests (1 hour)

**7.1 Create SmallTalkServiceTests.cs**

Test cases:
1. **Greeting detection** - "hi sốp" → Greeting intent
2. **Check-in detection** - "có ai không" → CheckIn intent
3. **Business intent** - "cho em xem sản phẩm" → None (not small talk)
4. **Transition readiness** - After 3 turns → SoftOffer
5. **VIP greeting** - VIP customer → personalized greeting
6. **Returning customer** - Returning → "Vui được gặp lại"
7. **Time-aware greeting** - Morning → "Chào buổi sáng"
8. **Casual tone** - Excited emotion → "Alo bạn!"
9. **Buying signal transition** - Buying signal → ReadyForBusiness
10. **Confidence threshold** - Low confidence → not small talk

**7.2 Test structure example**
```csharp
[Fact]
public async Task AnalyzeAsync_CasualGreeting_ReturnsSmallTalkResponse()
{
    // Arrange
    var context = CreateSmallTalkContext(
        message: "hi sốp",
        emotion: CreateEmotionScore(EmotionType.Positive),
        isReturning: true,
        turnCount: 1);
    
    // Act
    var response = await _service.AnalyzeAsync(context);
    
    // Assert
    Assert.True(response.IsSmallTalk);
    Assert.Equal(SmallTalkIntent.Greeting, response.Intent);
    Assert.Equal(TransitionReadiness.StayInSmallTalk, response.TransitionReadiness);
    Assert.Contains("Chào", response.SuggestedResponse);
    Assert.Contains("😊", response.SuggestedResponse);
}
```

## Todo List

- [ ] Create SmallTalkIntent enum
- [ ] Create TransitionReadiness enum
- [ ] Create SmallTalkResponse model
- [ ] Create SmallTalkContext model
- [ ] Create TimeOfDay enum
- [ ] Create SmallTalkOptions configuration
- [ ] Update appsettings.json
- [ ] Implement SmallTalkDetector class
- [ ] Implement keyword sets (greeting, check-in, business)
- [ ] Implement DetectIntent method
- [ ] Create ISmallTalkService interface
- [ ] Implement SmallTalkService main logic
- [ ] Implement DetermineTransitionReadiness method
- [ ] Implement GenerateResponse method
- [ ] Implement GenerateGreeting method
- [ ] Add GetTimeOfDay helper
- [ ] Register services in Program.cs DI
- [ ] Integrate with SalesStateHandlerBase
- [ ] Write unit tests (10+ test cases)
- [ ] Run tests and verify 85%+ coverage
- [ ] Manual testing with real conversations
- [ ] Code review and documentation

## Success Criteria

### Functional
- ✅ Small talk detection working for all intent types
- ✅ Natural responses generated (no forced business talk)
- ✅ Graceful transitions to business conversation
- ✅ Context-aware greetings (time, VIP, returning)
- ✅ Integration with SalesStateHandlerBase complete

### Performance
- ✅ Response time < 100ms (p95)
- ✅ No performance regression in overall flow
- ✅ Memory usage acceptable

### Quality
- ✅ Unit test coverage ≥ 85%
- ✅ All tests passing
- ✅ Small talk detection accuracy: 90%+
- ✅ Response naturalness: 85%+ (manual validation)
- ✅ Code review approved

## Risk Assessment

### High Risk
1. **Over-Detection of Small Talk**
   - Risk: Business inquiries misclassified as small talk
   - Likelihood: Medium | Impact: High
   - Mitigation: Business keywords take precedence, confidence threshold
   - Contingency: Tune keyword sets based on production data

### Medium Risk
2. **Awkward Transitions**
   - Risk: Transition from small talk to business feels forced
   - Likelihood: Medium | Impact: Medium
   - Mitigation: Soft offers ("Có gì em giúp không?"), gradual transition
   - Contingency: A/B test different transition phrases

3. **Repetitive Responses**
   - Risk: Same greeting every time feels robotic
   - Likelihood: Low | Impact: Medium
   - Mitigation: Multiple greeting templates, context-aware variation
   - Contingency: Add response variation in Phase 5

### Low Risk
4. **Performance Impact**
   - Risk: Small talk detection adds latency
   - Likelihood: Low | Impact: Low
   - Mitigation: Simple keyword matching (< 10ms), no ML
   - Contingency: Disable feature via configuration

## Security Considerations

- Input validation: Sanitize message text before analysis
- Logging: Don't log sensitive customer data
- Configuration: Validate SmallTalkOptions on startup
- Rate limiting: Prevent abuse of detection endpoint

## Performance Considerations

- **Keyword matching**: Use HashSet for O(1) lookup
- **No ML inference**: Rule-based only, < 10ms overhead
- **Async operations**: All methods async to avoid blocking
- **Memory**: Minimal memory footprint (keyword sets are static)

## Integration Points

### Upstream Dependencies
- **EmotionDetectionService** (Phase 1): Emotion for tone matching
- **ToneMatchingService** (Phase 2): Tone profile for response style
- **ConversationContextAnalyzer** (Phase 3): Journey stage, buying signals
- **CustomerIntelligenceService** (existing): VIP profile, returning customer

### Downstream Consumers
- **SalesStateHandlerBase** (existing): Use small talk response or inject context
- **Phase 5: ResponseValidator**: Validate small talk responses
- **Future: Analytics Dashboard**: Track small talk engagement metrics

## Testing Strategy

### Unit Tests (85%+ coverage)
- All public methods tested
- Edge cases: empty message, mixed intent, long messages
- Intent detection for each type
- Transition readiness logic
- Response generation variations

### Integration Tests (Phase 6)
- End-to-end flow: Greeting → Small talk → Business
- Verify natural conversation flow
- Test with real Vietnamese conversations
- Performance testing

### Manual Testing
- Test with real casual greetings
- Verify responses feel natural to native speakers
- Test transition timing (not too early, not too late)
- Validate VIP/returning customer personalization

## Migration & Backwards Compatibility

**No breaking changes:**
- New service, no existing code depends on it
- SalesStateHandlerBase modification is additive
- Existing greeting logic in CompleteStateHandler unchanged
- Can be disabled via configuration

**Rollback plan:**
- Remove SmallTalkService from DI registration
- Remove small talk detection code from SalesStateHandlerBase
- System falls back to existing behavior

**Feature flag:**
- Add `EnableSmallTalkDetection` flag in appsettings.json
- Easy to disable if issues arise in production

## Next Steps

After Phase 4 completion:
1. Collect production metrics: small talk detection accuracy, transition timing
2. Tune keyword sets based on false positives/negatives
3. Move to Phase 5: Response Validation
4. Add response variation templates (future enhancement)
5. Consider ML-based intent detection (post-MVP)

## Unresolved Questions

1. **Max small talk turns**: Should we limit to 3 turns or allow more?
   - **Recommendation**: Start with 3, tune based on user feedback

2. **Emoji usage**: Should all responses include emoji or only casual ones?
   - **Recommendation**: Use emoji for Positive/Excited emotions, skip for Neutral/Negative

3. **Response variation**: How many greeting templates do we need?
   - **Recommendation**: Start with 3-5 per intent type, expand based on repetition feedback

4. **Business keyword expansion**: How comprehensive should business keyword list be?
   - **Recommendation**: Start with core 20-30 keywords, expand based on false positives

5. **Integration with GreetingStateHandler**: Should we refactor GreetingStateHandler to use SmallTalkService?
   - **Recommendation**: Yes, but defer to Phase 6 integration work to avoid scope creep
