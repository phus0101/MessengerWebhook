# Phase 2: Tone Matching Service

**Status:** ✅ completed  
**Priority:** high  
**Timeline:** Days 4-5  
**Dependencies:** Phase 1 (Emotion Detection Service)  
**Completed:** 2026-04-07

## Context Links

- Plan Overview: `plan.md`
- Previous Phase: `phase-01-emotion-detection-service.md`
- Related: Phase 0 (Foundation & Personality) - completed
- Emotion Detection Service: `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs`
- Customer Intelligence: `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs`

## Overview

Implement dynamic tone adaptation service that adjusts bot response tone based on customer emotion + context (VIP tier, order history, conversation state). Service selects appropriate Vietnamese pronouns (anh/chị/em/bạn) and tone level (formal/friendly/casual) to mirror customer communication style.

**Priority:** High - Core naturalness feature  
**Current Status:** Not started  
**Estimated Effort:** 4-5 hours

## Key Insights

From Phase 1 completion:
- EmotionDetectionService operational with 5 emotion types (Positive, Neutral, Negative, Frustrated, Excited)
- Context-aware detection with escalation patterns (neutral→frustrated, satisfaction_drop, anger_escalation)
- Performance: < 100ms, caching enabled

From existing codebase:
- VipProfile has 3 tiers: Standard, Returning, Vip
- GreetingStyle currently hardcoded strings ("VIP_WARM_GREETING", "RETURNING_FRIENDLY_GREETING", "STANDARD_GREETING")
- CustomerIntelligence tracks TotalOrders, LifetimeValue, LastInteractionAt

Vietnamese pronoun selection critical:
- **anh**: older male, formal respect
- **chị**: older female, formal respect  
- **em**: younger person, warm/friendly
- **bạn**: neutral, safe default when uncertain

## Requirements

### Functional Requirements

1. **Tone Profile Generation**
   - Input: EmotionScore + VipProfile + conversation context
   - Output: ToneProfile with pronoun + tone level + escalation flags
   - Support 3 tone levels: Formal, Friendly, Casual
   - Pronoun selection based on customer age/gender/tier (default: "bạn")

2. **Emotion-Based Tone Adaptation**
   - Positive/Excited → Friendly/Casual tone, enthusiastic language
   - Neutral → Match customer's tone (mirror)
   - Negative → Empathetic, solution-focused
   - Frustrated → Apologetic, escalation-ready, offer human handoff

3. **Customer Context Integration**
   - VIP tier influences formality (VIP → more formal)
   - Returning customers → warmer, less formal
   - New customers → neutral, professional
   - High-risk customers → extra careful, formal

4. **Escalation Handling**
   - Detect emotion escalation from Phase 1 (neutral→frustrated, anger_escalation)
   - Flag for human handoff when frustration detected
   - Adjust tone to de-escalate (apologetic, solution-focused)

### Non-Functional Requirements

- Performance: < 50ms tone profile generation
- Accuracy: 90%+ tone appropriateness, 95%+ pronoun selection
- Maintainability: Easy to add new tone rules
- Testability: Deterministic output for given inputs

## Architecture

### Component Design

```
ToneMatchingService
├── Input: EmotionScore, VipProfile, ConversationContext
├── Processing:
│   ├── AnalyzeCustomerContext (tier, orders, risk)
│   ├── MapEmotionToTone (emotion → tone level)
│   ├── SelectPronoun (context → anh/chị/em/bạn)
│   ├── DetectEscalation (emotion metadata)
│   └── BuildToneProfile (combine all signals)
└── Output: ToneProfile
```

### Data Flow

```
User Message 
→ EmotionDetectionService (Phase 1) 
→ EmotionScore
→ ToneMatchingService + VipProfile + Context
→ ToneProfile
→ SalesStateHandlerBase (inject into prompt)
→ GeminiService (generate response with tone)
→ Response to user
```

### New Files Structure

```
src/MessengerWebhook/Services/Tone/
├── IToneMatchingService.cs              # Interface
├── ToneMatchingService.cs               # Main implementation
├── Models/
│   ├── ToneProfile.cs                   # Output model
│   ├── ToneLevel.cs                     # Enum: Formal, Friendly, Casual
│   ├── VietnamesePronoun.cs             # Enum: Anh, Chi, Em, Ban
│   └── ToneContext.cs                   # Input context aggregator
└── Configuration/
    └── ToneMatchingOptions.cs           # Configuration
```

## Related Code Files

### Files to Create

1. `src/MessengerWebhook/Services/Tone/IToneMatchingService.cs`
2. `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs`
3. `src/MessengerWebhook/Services/Tone/Models/ToneProfile.cs`
4. `src/MessengerWebhook/Services/Tone/Models/ToneLevel.cs`
5. `src/MessengerWebhook/Services/Tone/Models/VietnamesePronoun.cs`
6. `src/MessengerWebhook/Services/Tone/Models/ToneContext.cs`
7. `src/MessengerWebhook/Services/Tone/Configuration/ToneMatchingOptions.cs`
8. `tests/MessengerWebhook.UnitTests/Services/Tone/ToneMatchingServiceTests.cs`

### Files to Modify

1. `src/MessengerWebhook/Program.cs` - Register ToneMatchingService in DI
2. `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` - Integrate tone profile into prompt
3. `src/MessengerWebhook/appsettings.json` - Add tone matching configuration

### Files to Read (Dependencies)

- `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs` - Emotion input
- `src/MessengerWebhook/Services/Emotion/Models/EmotionScore.cs` - Emotion model
- `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs` - Customer context
- `src/MessengerWebhook/Data/Entities/VipProfile.cs` - VIP tier data
- `src/MessengerWebhook/Data/Entities/CustomerIdentity.cs` - Customer data

## Implementation Steps

### Step 1: Create Models & Enums (45 min)

**1.1 Create ToneLevel enum**
```csharp
public enum ToneLevel
{
    Formal,      // Trang trọng, lịch sự (VIP, new customers)
    Friendly,    // Thân thiện, gần gũi (returning customers)
    Casual       // Thoải mái, vui vẻ (excited customers, close relationship)
}
```

**1.2 Create VietnamesePronoun enum**
```csharp
public enum VietnamesePronoun
{
    Anh,    // Older male, formal respect
    Chi,    // Older female, formal respect
    Em,     // Younger person, warm/friendly
    Ban     // Neutral, safe default
}
```

**1.3 Create ToneProfile model**
```csharp
public class ToneProfile
{
    public ToneLevel Level { get; set; }
    public VietnamesePronoun Pronoun { get; set; }
    public string PronounText { get; set; }  // "anh", "chị", "em", "bạn"
    public bool RequiresEscalation { get; set; }
    public string? EscalationReason { get; set; }
    public Dictionary<string, string> ToneInstructions { get; set; }  // For prompt injection
    public Dictionary<string, object> Metadata { get; set; }
}
```

**1.4 Create ToneContext model** (input aggregator)
```csharp
public class ToneContext
{
    public EmotionScore Emotion { get; set; }
    public VipProfile VipProfile { get; set; }
    public CustomerIdentity Customer { get; set; }
    public int ConversationTurnCount { get; set; }
    public bool IsFirstInteraction { get; set; }
}
```

### Step 2: Create Configuration (15 min)

**2.1 Create ToneMatchingOptions.cs**
```csharp
public class ToneMatchingOptions
{
    public bool EnableEmotionBasedAdaptation { get; set; } = true;
    public bool EnableEscalationDetection { get; set; } = true;
    public double FrustrationEscalationThreshold { get; set; } = 0.7;
    public string DefaultPronoun { get; set; } = "bạn";
    public bool EnableCaching { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 5;
}
```

**2.2 Update appsettings.json**
```json
"ToneMatching": {
    "EnableEmotionBasedAdaptation": true,
    "EnableEscalationDetection": true,
    "FrustrationEscalationThreshold": 0.7,
    "DefaultPronoun": "bạn",
    "EnableCaching": true,
    "CacheDurationMinutes": 5
}
```

### Step 3: Implement Service Interface (15 min)

**3.1 Create IToneMatchingService.cs**
```csharp
public interface IToneMatchingService
{
    Task<ToneProfile> GenerateToneProfileAsync(
        ToneContext context,
        CancellationToken cancellationToken = default);
    
    Task<ToneProfile> GenerateToneProfileAsync(
        EmotionScore emotion,
        VipProfile vipProfile,
        CustomerIdentity customer,
        int conversationTurnCount = 0,
        CancellationToken cancellationToken = default);
}
```

### Step 4: Implement Core Service Logic (2 hours)

**4.1 Create ToneMatchingService.cs skeleton**
- Constructor with dependencies: IMemoryCache, ILogger, IOptions<ToneMatchingOptions>
- Implement both interface methods (second calls first with ToneContext wrapper)

**4.2 Implement AnalyzeCustomerContext method**
```csharp
private CustomerContextSignals AnalyzeCustomerContext(ToneContext context)
{
    // Analyze VIP tier
    var isVip = context.VipProfile.Tier == VipTier.Vip;
    var isReturning = context.VipProfile.Tier == VipTier.Returning;
    var isNew = context.VipProfile.Tier == VipTier.Standard;
    
    // Analyze order history
    var hasOrders = context.Customer.TotalOrders > 0;
    var isHighValue = context.Customer.LifetimeValue > 1000000; // 1M VND
    
    // Analyze risk
    var hasFailedDeliveries = context.Customer.FailedDeliveries > 0;
    var riskScore = hasOrders 
        ? context.Customer.FailedDeliveries / (decimal)context.Customer.TotalOrders 
        : 0;
    
    return new CustomerContextSignals
    {
        IsVip = isVip,
        IsReturning = isReturning,
        IsNew = isNew,
        HasOrders = hasOrders,
        IsHighValue = isHighValue,
        RiskScore = riskScore,
        IsFirstInteraction = context.IsFirstInteraction
    };
}
```

**4.3 Implement MapEmotionToToneLevel method**
```csharp
private ToneLevel MapEmotionToToneLevel(
    EmotionScore emotion, 
    CustomerContextSignals context)
{
    // VIP always gets Formal unless Excited
    if (context.IsVip && emotion.PrimaryEmotion != EmotionType.Excited)
        return ToneLevel.Formal;
    
    // Emotion-based mapping
    return emotion.PrimaryEmotion switch
    {
        EmotionType.Positive => context.IsReturning ? ToneLevel.Friendly : ToneLevel.Formal,
        EmotionType.Excited => ToneLevel.Casual,
        EmotionType.Neutral => context.IsNew ? ToneLevel.Formal : ToneLevel.Friendly,
        EmotionType.Negative => ToneLevel.Formal,  // Professional, empathetic
        EmotionType.Frustrated => ToneLevel.Formal, // Apologetic, careful
        _ => ToneLevel.Friendly
    };
}
```

**4.4 Implement SelectPronoun method**
```csharp
private VietnamesePronoun SelectPronoun(
    CustomerContextSignals context,
    ToneLevel toneLevel)
{
    // Default to neutral "bạn" when uncertain
    // In production, this would use customer age/gender from profile
    // For now, use tier + tone as proxy
    
    if (context.IsVip)
        return VietnamesePronoun.Chi; // Respectful default for VIP
    
    if (toneLevel == ToneLevel.Casual && context.IsReturning)
        return VietnamesePronoun.Ban; // Casual but safe
    
    if (toneLevel == ToneLevel.Friendly)
        return VietnamesePronoun.Ban; // Neutral friendly
    
    return VietnamesePronoun.Ban; // Safe default
}
```

**4.5 Implement DetectEscalation method**
```csharp
private (bool requiresEscalation, string? reason) DetectEscalation(
    EmotionScore emotion,
    ToneMatchingOptions options)
{
    if (!options.EnableEscalationDetection)
        return (false, null);
    
    // Check for frustration above threshold
    if (emotion.PrimaryEmotion == EmotionType.Frustrated && 
        emotion.Confidence >= options.FrustrationEscalationThreshold)
    {
        return (true, "Customer is frustrated - consider human handoff");
    }
    
    // Check for escalation patterns from emotion metadata
    if (emotion.Metadata.TryGetValue("escalation", out var escalation))
    {
        var pattern = escalation.ToString();
        return pattern switch
        {
            "anger_escalation" => (true, "Anger escalation detected - immediate attention needed"),
            "neutral_to_frustrated" => (true, "Customer frustration increasing - consider escalation"),
            "satisfaction_drop" => (true, "Customer satisfaction dropping - proactive intervention needed"),
            _ => (false, null)
        };
    }
    
    return (false, null);
}
```

**4.6 Implement BuildToneInstructions method**
```csharp
private Dictionary<string, string> BuildToneInstructions(
    ToneLevel level,
    EmotionType emotion,
    bool requiresEscalation)
{
    var instructions = new Dictionary<string, string>();
    
    // Base tone instruction
    instructions["tone_level"] = level switch
    {
        ToneLevel.Formal => "Sử dụng ngôn ngữ trang trọng, lịch sự, chuyên nghiệp",
        ToneLevel.Friendly => "Sử dụng ngôn ngữ thân thiện, gần gũi nhưng vẫn lịch sự",
        ToneLevel.Casual => "Sử dụng ngôn ngữ thoải mái, vui vẻ, gần gũi",
        _ => "Sử dụng ngôn ngữ thân thiện, lịch sự"
    };
    
    // Emotion-specific instruction
    instructions["emotion_adaptation"] = emotion switch
    {
        EmotionType.Positive => "Khách hàng đang vui vẻ - hãy duy trì năng lượng tích cực",
        EmotionType.Excited => "Khách hàng đang phấn khích - hãy nhiệt tình và hào hứng",
        EmotionType.Neutral => "Khách hàng bình thường - hãy chuyên nghiệp và hiệu quả",
        EmotionType.Negative => "Khách hàng không hài lòng - hãy thấu hiểu và tập trung giải pháp",
        EmotionType.Frustrated => "Khách hàng bực bội - hãy xin lỗi chân thành và đề xuất giải pháp cụ thể",
        _ => "Hãy thân thiện và chuyên nghiệp"
    };
    
    // Escalation instruction
    if (requiresEscalation)
    {
        instructions["escalation"] = "QUAN TRỌNG: Khách hàng cần được chăm sóc đặc biệt. Hãy đề xuất chuyển cho nhân viên nếu không giải quyết được ngay.";
    }
    
    return instructions;
}
```

**4.7 Implement main GenerateToneProfileAsync method**
```csharp
public async Task<ToneProfile> GenerateToneProfileAsync(
    ToneContext context,
    CancellationToken cancellationToken = default)
{
    // Check cache
    if (_options.EnableCaching)
    {
        var cacheKey = GetCacheKey(context);
        if (_cache.TryGetValue<ToneProfile>(cacheKey, out var cached))
        {
            _logger.LogDebug("Tone profile cache hit");
            return cached!;
        }
    }
    
    // Analyze customer context
    var customerSignals = AnalyzeCustomerContext(context);
    
    // Map emotion to tone level
    var toneLevel = MapEmotionToToneLevel(context.Emotion, customerSignals);
    
    // Select pronoun
    var pronoun = SelectPronoun(customerSignals, toneLevel);
    var pronounText = GetPronounText(pronoun);
    
    // Detect escalation
    var (requiresEscalation, escalationReason) = DetectEscalation(
        context.Emotion, 
        _options);
    
    // Build tone instructions
    var instructions = BuildToneInstructions(
        toneLevel, 
        context.Emotion.PrimaryEmotion, 
        requiresEscalation);
    
    // Create profile
    var profile = new ToneProfile
    {
        Level = toneLevel,
        Pronoun = pronoun,
        PronounText = pronounText,
        RequiresEscalation = requiresEscalation,
        EscalationReason = escalationReason,
        ToneInstructions = instructions,
        Metadata = new Dictionary<string, object>
        {
            ["emotion"] = context.Emotion.PrimaryEmotion.ToString(),
            ["emotion_confidence"] = context.Emotion.Confidence,
            ["vip_tier"] = context.VipProfile.Tier.ToString(),
            ["is_returning"] = customerSignals.IsReturning,
            ["conversation_turns"] = context.ConversationTurnCount
        }
    };
    
    // Cache result
    if (_options.EnableCaching)
    {
        var cacheKey = GetCacheKey(context);
        _cache.Set(cacheKey, profile, TimeSpan.FromMinutes(_options.CacheDurationMinutes));
    }
    
    _logger.LogInformation(
        "Generated tone profile: {Level} / {Pronoun} (emotion: {Emotion}, escalation: {Escalation})",
        toneLevel,
        pronounText,
        context.Emotion.PrimaryEmotion,
        requiresEscalation);
    
    return profile;
}
```

### Step 5: Register Service in DI (15 min)

**5.1 Update Program.cs**
```csharp
// Add after EmotionDetectionService registration
builder.Services.Configure<ToneMatchingOptions>(
    builder.Configuration.GetSection("ToneMatching"));
builder.Services.AddSingleton<IToneMatchingService, ToneMatchingService>();
```

### Step 6: Integration with SalesStateHandlerBase (30 min)

**6.1 Inject IToneMatchingService into SalesStateHandlerBase**
- Add constructor parameter
- Store as private field

**6.2 Update BuildSystemPrompt or similar method**
```csharp
// After emotion detection
var emotion = await _emotionDetectionService
    .DetectEmotionWithContextAsync(message, history, cancellationToken);

// Generate tone profile
var vipProfile = await _customerIntelligenceService
    .GetVipProfileAsync(customer, cancellationToken);

var toneProfile = await _toneMatchingService.GenerateToneProfileAsync(
    emotion,
    vipProfile,
    customer,
    conversationTurnCount: history.Count,
    cancellationToken);

// Inject into prompt
var toneInstruction = string.Join("\n", 
    toneProfile.ToneInstructions.Select(kv => $"- {kv.Value}"));

var systemPrompt = $@"
{basePrompt}

## Tone Adaptation
Xưng hô: {toneProfile.PronounText}
{toneInstruction}

{restOfPrompt}
";

// Store in context for logging
ctx.SetData("toneProfile", toneProfile);
```

### Step 7: Unit Tests (1.5 hours)

**7.1 Create ToneMatchingServiceTests.cs**

Test cases:
1. **VIP customer + Positive emotion → Formal tone + Chi pronoun**
2. **Returning customer + Excited emotion → Casual tone + Ban pronoun**
3. **New customer + Neutral emotion → Formal tone + Ban pronoun**
4. **Any customer + Frustrated emotion (high confidence) → Escalation flag**
5. **Emotion escalation pattern → Escalation flag with reason**
6. **Negative emotion → Formal tone + empathetic instruction**
7. **Caching works correctly** (same input returns cached result)
8. **Pronoun selection defaults to Ban when uncertain**
9. **Tone instructions contain correct Vietnamese text**
10. **Metadata populated correctly**

**7.2 Test structure example**
```csharp
[Fact]
public async Task GenerateToneProfile_VipCustomerPositiveEmotion_ReturnsFormalTone()
{
    // Arrange
    var emotion = CreateEmotionScore(EmotionType.Positive, 0.8);
    var vipProfile = CreateVipProfile(VipTier.Vip);
    var customer = CreateCustomer(totalOrders: 10);
    
    // Act
    var profile = await _service.GenerateToneProfileAsync(
        emotion, vipProfile, customer);
    
    // Assert
    Assert.Equal(ToneLevel.Formal, profile.Level);
    Assert.Equal(VietnamesePronoun.Chi, profile.Pronoun);
    Assert.False(profile.RequiresEscalation);
    Assert.Contains("trang trọng", profile.ToneInstructions["tone_level"]);
}
```

## Todo List

- [ ] Create ToneLevel enum
- [ ] Create VietnamesePronoun enum
- [ ] Create ToneProfile model
- [ ] Create ToneContext model
- [ ] Create ToneMatchingOptions configuration
- [ ] Update appsettings.json with ToneMatching section
- [ ] Create IToneMatchingService interface
- [ ] Implement ToneMatchingService core logic
- [ ] Implement AnalyzeCustomerContext method
- [ ] Implement MapEmotionToToneLevel method
- [ ] Implement SelectPronoun method
- [ ] Implement DetectEscalation method
- [ ] Implement BuildToneInstructions method
- [ ] Implement GenerateToneProfileAsync method
- [ ] Add caching logic
- [ ] Register service in Program.cs DI container
- [ ] Integrate with SalesStateHandlerBase
- [ ] Write unit tests (10 test cases minimum)
- [ ] Run tests and verify 85%+ coverage
- [ ] Manual testing with different emotion + customer combinations
- [ ] Performance testing (< 50ms target)
- [ ] Code review and documentation

## Success Criteria

### Functional
- ✅ Tone profile generation working for all emotion types
- ✅ Pronoun selection appropriate for customer context
- ✅ Escalation detection working for frustrated customers
- ✅ Tone instructions in Vietnamese, grammatically correct
- ✅ Integration with SalesStateHandlerBase complete

### Performance
- ✅ Response time < 50ms (p95)
- ✅ Caching working correctly
- ✅ No memory leaks

### Quality
- ✅ Unit test coverage ≥ 85%
- ✅ All tests passing
- ✅ Tone matching appropriateness: 90%+ (manual validation)
- ✅ Pronoun selection correct: 95%+ (manual validation)
- ✅ Code review approved

## Risk Assessment

### High Risk
1. **Pronoun Selection Accuracy**
   - Risk: Wrong pronoun offends customer or sounds unnatural
   - Likelihood: Medium | Impact: High
   - Mitigation: Default to neutral "bạn" when uncertain, add customer age/gender to profile in future
   - Contingency: Allow manual override in admin UI

### Medium Risk
2. **Tone Drift in Long Conversations**
   - Risk: Tone changes mid-conversation feel inconsistent
   - Likelihood: Medium | Impact: Medium
   - Mitigation: Cache tone profile per conversation session, only update on significant emotion shift
   - Contingency: Add tone consistency validation in Phase 5

3. **Over-Escalation**
   - Risk: Too many false positive escalations annoy customers
   - Likelihood: Low | Impact: Medium
   - Mitigation: High confidence threshold (0.7), require escalation pattern not just single frustrated message
   - Contingency: Tune threshold based on production metrics

### Low Risk
4. **Performance Impact**
   - Risk: Tone matching adds latency
   - Likelihood: Low | Impact: Low
   - Mitigation: Caching, simple rule-based logic (no ML), < 50ms target
   - Contingency: Disable caching if issues, optimize hot paths

## Security Considerations

- Input validation: Sanitize customer data before processing
- Logging: Don't log sensitive customer info (phone, address)
- Configuration: Validate ToneMatchingOptions on startup
- Caching: Use memory cache (not distributed) to avoid data leaks

## Performance Considerations

- **Caching strategy**: Cache by (emotion + vipTier + conversationTurn) key for 5 minutes
- **Memory usage**: ToneProfile is small (~1KB), cache max 1000 entries
- **Async operations**: All methods async to avoid blocking
- **Hot path optimization**: Emotion→Tone mapping is O(1) switch statement

## Integration Points

### Upstream Dependencies
- **EmotionDetectionService** (Phase 1): Provides EmotionScore input
- **CustomerIntelligenceService** (existing): Provides VipProfile + CustomerIdentity
- **ConversationSession** (existing): Provides conversation turn count

### Downstream Consumers
- **SalesStateHandlerBase** (existing): Injects ToneProfile into system prompt
- **Phase 3: ConversationContextAnalyzer**: Will use ToneProfile for context analysis
- **Phase 5: ResponseValidator**: Will validate tone consistency

## Testing Strategy

### Unit Tests (85%+ coverage)
- All public methods tested
- Edge cases: null inputs, empty context, extreme values
- Emotion type combinations
- VIP tier combinations
- Escalation scenarios

### Integration Tests (Phase 6)
- End-to-end flow: Message → Emotion → Tone → Response
- Verify tone instructions appear in generated responses
- Verify pronoun used correctly in responses

### Manual Testing
- Test with real Vietnamese messages
- Verify tone feels natural to native speakers
- Test escalation flow with frustrated messages

## Migration & Backwards Compatibility

**No breaking changes:**
- New service, no existing code depends on it
- SalesStateHandlerBase modification is additive (new prompt section)
- Existing GreetingStyle in VipProfile remains unchanged (will be deprecated in Phase 6)

**Rollback plan:**
- Remove ToneMatchingService from DI registration
- Remove tone injection code from SalesStateHandlerBase
- System falls back to existing behavior

## Next Steps

After Phase 2 completion:
1. Collect production metrics: tone appropriateness, escalation rate
2. Tune escalation threshold based on false positive rate
3. Move to Phase 3: Conversation Context Analyzer
4. Add customer age/gender to profile for better pronoun selection (future enhancement)

## Unresolved Questions

1. **Customer age/gender data**: Currently not in CustomerIdentity table. Need to add fields or default to neutral pronoun?
   - **Decision needed**: Add fields now or defer to future phase?
   - **Recommendation**: Defer, use neutral "bạn" default for MVP

2. **Tone consistency validation**: Should we prevent tone from changing mid-conversation?
   - **Decision needed**: Lock tone per session or allow dynamic adaptation?
   - **Recommendation**: Allow dynamic but log tone changes for Phase 5 validation

3. **Escalation action**: What happens after escalation flag is set?
   - **Decision needed**: Auto-notify human agent or just flag in response?
   - **Recommendation**: Phase 2 just flags, Phase 4 implements handoff logic

4. **Pronoun override**: Should admin UI allow manual pronoun selection per customer?
   - **Decision needed**: Build now or defer?
   - **Recommendation**: Defer to post-MVP, add to backlog
