# Phase 1: Emotion Detection Service

**Status:** pending  
**Priority:** high  
**Timeline:** Days 3-4  
**Dependencies:** Phase 0 (Foundation & Personality)

## Context Links

- Plan Overview: `plan.md`
- Research Report: `plans/reports/researcher-emotion-detection-260406-2036.md`
- Previous Phase: `phase-00-foundation-personality.md`

## Overview

Implement hybrid emotion detection service (rule-based + ML ready) để nhận biết cảm xúc khách hàng từ tin nhắn. Service này sẽ phân tích tone, từ ngữ, và context để xác định emotion state (positive, neutral, negative, frustrated, excited).

**Priority:** High - Foundation cho tone matching và response adaptation  
**Current Status:** Not started

## Key Insights

Từ research report:
- Hybrid approach (rule-based + ML) đạt 85%+ accuracy
- Rule-based baseline đủ cho MVP, ML model là optional enhancement
- Vietnamese emotion keywords cần xử lý đặc biệt (diacritics, slang)
- Response time target: < 100ms cho real-time chat

## Requirements

### Functional Requirements

1. **Emotion Detection**
   - Detect 5 emotion states: Positive, Neutral, Negative, Frustrated, Excited
   - Support Vietnamese text với diacritics
   - Handle casual language và slang
   - Confidence score cho mỗi detection

2. **Rule-Based Engine**
   - Keyword matching với Vietnamese emotion lexicon
   - Punctuation analysis (!!!, ???, ...)
   - Emoji detection và interpretation
   - Negation handling ("không vui", "chẳng tốt")

3. **ML Integration Point**
   - Interface ready cho future ML model
   - Fallback to rule-based khi ML unavailable
   - A/B testing capability

### Non-Functional Requirements

- Performance: < 100ms response time
- Accuracy: 85%+ baseline với rule-based
- Scalability: Handle 1000+ requests/second
- Maintainability: Easy to add new emotion keywords

## Architecture

### New Files

```
src/MessengerWebhook/Services/Emotion/
├── IEmotionDetectionService.cs          # Interface
├── EmotionDetectionService.cs           # Main implementation
├── RuleBasedEmotionDetector.cs          # Rule-based engine
├── Models/
│   ├── EmotionScore.cs                  # Emotion result model
│   ├── EmotionType.cs                   # Enum: Positive, Neutral, etc.
│   └── EmotionKeywords.cs               # Vietnamese emotion lexicon
└── Configuration/
    └── EmotionDetectionOptions.cs       # Configuration options
```

### Service Interface

```csharp
public interface IEmotionDetectionService
{
    Task<EmotionScore> DetectEmotionAsync(
        string message, 
        CancellationToken cancellationToken = default);
    
    Task<EmotionScore> DetectEmotionWithContextAsync(
        string message,
        List<AiConversationMessage> history,
        CancellationToken cancellationToken = default);
}

public class EmotionScore
{
    public EmotionType PrimaryEmotion { get; set; }
    public Dictionary<EmotionType, double> Scores { get; set; }
    public double Confidence { get; set; }
    public string DetectionMethod { get; set; } // "rule-based" or "ml"
}

public enum EmotionType
{
    Positive,    // Vui vẻ, hài lòng
    Neutral,     // Bình thường
    Negative,    // Không hài lòng
    Frustrated,  // Bực bội, tức giận
    Excited      // Phấn khích, háo hức
}
```

## Related Code Files

### Files to Create

1. `src/MessengerWebhook/Services/Emotion/IEmotionDetectionService.cs`
2. `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs`
3. `src/MessengerWebhook/Services/Emotion/RuleBasedEmotionDetector.cs`
4. `src/MessengerWebhook/Services/Emotion/Models/EmotionScore.cs`
5. `src/MessengerWebhook/Services/Emotion/Models/EmotionType.cs`
6. `src/MessengerWebhook/Services/Emotion/Models/EmotionKeywords.cs`
7. `src/MessengerWebhook/Services/Emotion/Configuration/EmotionDetectionOptions.cs`

### Files to Modify

1. `src/MessengerWebhook/Program.cs` - Register EmotionDetectionService
2. `src/MessengerWebhook/Configuration/SalesBotOptions.cs` - Add emotion detection config
3. `src/MessengerWebhook/appsettings.json` - Add emotion detection settings

## Implementation Steps

### Step 1: Create Models & Enums (30 min)

1. Create `EmotionType.cs` enum với 5 emotion states
2. Create `EmotionScore.cs` model với confidence scores
3. Create `EmotionKeywords.cs` với Vietnamese emotion lexicon:
   - Positive keywords: "vui", "tốt", "hay", "ok", "được", "ổn"
   - Negative keywords: "tệ", "dở", "không tốt", "kém"
   - Frustrated keywords: "bực", "tức", "chán", "mệt"
   - Excited keywords: "wow", "tuyệt", "xuất sắc", "quá đỉnh"

### Step 2: Implement Rule-Based Detector (1 hour)

1. Create `RuleBasedEmotionDetector.cs`:
   - Keyword matching với case-insensitive
   - Punctuation analysis (count !, ?, ...)
   - Emoji detection (😊, 😢, 😡, 🎉)
   - Negation handling ("không" + positive word = negative)
   - Calculate confidence score based on signal strength

2. Scoring algorithm:
   ```
   confidence = (keyword_matches * 0.4) + 
                (punctuation_score * 0.3) + 
                (emoji_score * 0.3)
   ```

### Step 3: Implement Main Service (1 hour)

1. Create `EmotionDetectionService.cs`:
   - Inject `RuleBasedEmotionDetector`
   - Implement `DetectEmotionAsync` method
   - Implement `DetectEmotionWithContextAsync` (consider previous messages)
   - Add caching cho repeated messages
   - Add logging cho debugging

2. Context-aware detection:
   - Analyze last 3 messages for emotion trend
   - Weight recent messages higher
   - Detect emotion escalation (neutral → frustrated)

### Step 4: Configuration & DI (30 min)

1. Create `EmotionDetectionOptions.cs`:
   ```csharp
   public class EmotionDetectionOptions
   {
       public bool EnableContextAnalysis { get; set; } = true;
       public int ContextWindowSize { get; set; } = 3;
       public double ConfidenceThreshold { get; set; } = 0.6;
       public bool EnableCaching { get; set; } = true;
   }
   ```

2. Update `Program.cs`:
   ```csharp
   builder.Services.Configure<EmotionDetectionOptions>(
       builder.Configuration.GetSection("EmotionDetection"));
   builder.Services.AddSingleton<IEmotionDetectionService, EmotionDetectionService>();
   ```

3. Update `appsettings.json`:
   ```json
   "EmotionDetection": {
       "EnableContextAnalysis": true,
       "ContextWindowSize": 3,
       "ConfidenceThreshold": 0.6,
       "EnableCaching": true
   }
   ```

### Step 5: Unit Tests (1 hour)

1. Create `tests/MessengerWebhook.UnitTests/Services/Emotion/EmotionDetectionServiceTests.cs`
2. Test cases:
   - Positive emotion detection: "Tuyệt vời quá!"
   - Negative emotion detection: "Dở quá, không thích"
   - Frustrated emotion detection: "Bực mình quá!!!"
   - Excited emotion detection: "Wow quá đỉnh 🎉"
   - Neutral emotion detection: "Oke"
   - Negation handling: "Không vui lắm"
   - Context-aware detection: emotion escalation
   - Edge cases: empty string, special characters

### Step 6: Integration Point (30 min)

1. Add integration point trong `SalesStateHandlerBase.cs`:
   ```csharp
   var emotion = await _emotionDetectionService
       .DetectEmotionWithContextAsync(message, history, cancellationToken);
   
   // Store emotion in context for tone matching
   ctx.SetData("currentEmotion", emotion);
   ```

2. Prepare for Phase 2 (Tone Matching Service) integration

## Todo List

- [ ] Create EmotionType enum
- [ ] Create EmotionScore model
- [ ] Create EmotionKeywords lexicon
- [ ] Implement RuleBasedEmotionDetector
- [ ] Implement EmotionDetectionService
- [ ] Add configuration options
- [ ] Register service in DI container
- [ ] Write unit tests (85%+ coverage)
- [ ] Add integration point in SalesStateHandlerBase
- [ ] Performance testing (< 100ms target)
- [ ] Documentation và code comments

## Success Criteria

### Functional
- ✅ Emotion detection working cho 5 emotion types
- ✅ Vietnamese keyword matching accurate
- ✅ Emoji detection working
- ✅ Negation handling correct
- ✅ Context-aware detection implemented

### Performance
- ✅ Response time < 100ms (p95)
- ✅ Accuracy ≥ 85% on test dataset
- ✅ No memory leaks
- ✅ Caching working correctly

### Quality
- ✅ Unit test coverage ≥ 85%
- ✅ All tests passing
- ✅ Code review approved
- ✅ Documentation complete

## Risk Assessment

### High Risk
1. **Vietnamese Language Complexity**
   - Risk: Diacritics, slang, regional variations
   - Mitigation: Comprehensive keyword lexicon, normalize text before matching
   - Contingency: Expand lexicon based on production data

### Medium Risk
2. **Performance Impact**
   - Risk: Emotion detection adds latency to response time
   - Mitigation: Caching, async processing, optimize keyword matching
   - Contingency: Disable context analysis if needed

3. **False Positives/Negatives**
   - Risk: Misclassify emotion, especially sarcasm
   - Mitigation: Confidence threshold, context analysis
   - Contingency: ML model upgrade in future

### Low Risk
4. **Integration Complexity**
   - Risk: Breaking existing flow
   - Mitigation: Non-blocking integration, feature flag
   - Contingency: Easy rollback via configuration

## Security Considerations

- Input validation: Sanitize message text
- Rate limiting: Prevent abuse
- Logging: Don't log sensitive customer data
- Configuration: Secure emotion keywords file

## Performance Considerations

- Keyword matching: Use HashSet for O(1) lookup
- Caching: Cache emotion results for 5 minutes
- Async: All operations async to avoid blocking
- Memory: Limit context window size to prevent memory bloat

## Next Steps

After Phase 1 completion:
1. Review emotion detection accuracy metrics
2. Collect production data for ML training (optional)
3. Move to Phase 2: Tone Matching Service
4. Integrate emotion detection with tone adaptation

## Notes

- Rule-based approach đủ cho MVP, không cần ML ngay
- Focus on Vietnamese language specifics
- Keep interface flexible cho future ML integration
- Monitor accuracy metrics để quyết định khi nào cần ML model
