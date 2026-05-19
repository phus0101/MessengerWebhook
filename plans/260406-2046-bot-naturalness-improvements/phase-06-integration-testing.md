# Phase 6: Integration & Testing

**Status:** pending  
**Priority:** high  
**Timeline:** Days 7-8  
**Dependencies:** Phase 0-5 (All services implemented)

## Context Links

- Plan Overview: `plan.md`
- Previous Phase: `phase-05-response-validation.md`
- Integration Point: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Existing Tests: `tests/MessengerWebhook.UnitTests/Services/`
- Integration Tests: `tests/MessengerWebhook.IntegrationTests/`

## Overview

Comprehensive integration testing of the complete naturalness pipeline: Emotion Detection → Tone Matching → Context Analysis → Small Talk → Response Validation. Verify all services work together correctly, meet performance targets, handle errors gracefully, and maintain backward compatibility.

**Priority:** High - Quality gate before production  
**Current Status:** Not started  
**Estimated Effort:** 4-6 hours

## Key Insights

From completed phases:
- Phase 0: Foundation with personality traits and customer instruction
- Phase 1: EmotionDetectionService with 85%+ accuracy target
- Phase 2: ToneMatchingService with pronoun selection
- Phase 3: ConversationContextAnalyzer with journey stage tracking
- Phase 4: SmallTalkService with transition readiness
- Phase 5: ResponseValidationService with quality checks

Integration point (SalesStateHandlerBase.cs lines 524-620):
```
message → EmotionDetectionService.DetectEmotionWithContextAsync()
       → ConversationContextAnalyzer.AnalyzeWithEmotionAsync()
       → ToneMatchingService.GenerateToneProfileAsync()
       → SmallTalkService.AnalyzeAsync()
       → [AI generates response]
       → ResponseValidationService.ValidateAsync()
       → output
```

Performance targets:
- Emotion detection: < 100ms
- Context analysis: < 50ms for 10-turn history
- Tone matching: < 50ms
- Small talk: < 30ms
- Validation: < 50ms
- **Total overhead: < 100ms** (services run in sequence, some cached)

## Requirements

### Functional Requirements

1. **Pipeline Integration**
   - All services called in correct order
   - Data flows correctly between services
   - Context preserved across service calls
   - Cache working (IMemoryCache for emotion/tone)

2. **End-to-End Scenarios**
   - Greeting flow (new vs returning customer)
   - Browsing stage (casual questions)
   - Considering stage (detailed inquiries)
   - Ready to buy stage (order intent)
   - Error scenarios (service failures)

3. **Configuration Validation**
   - All Options classes validated on startup
   - Invalid configs rejected with clear errors
   - Default values work correctly
   - Feature flags respected

4. **Error Handling**
   - Graceful degradation when services fail
   - Fallback responses when validation fails
   - No cascading failures
   - Proper logging at all levels

### Non-Functional Requirements

- Performance: Total overhead < 100ms (P95)
- Reliability: 99.9% success rate
- Test coverage: ≥ 85% for new services
- Backward compatibility: Existing flows unaffected

## Architecture

### Test Pyramid

```
E2E Tests (5 scenarios)
├── Greeting: New customer
├── Greeting: Returning customer
├── Browsing: Product questions
├── Considering: Detailed inquiry
└── Error: Service failure

Integration Tests (10+ scenarios)
├── Full pipeline: Emotion → Tone → Context → SmallTalk → Validation
├── Cache behavior: Emotion/Tone caching
├── Error handling: Each service fails independently
├── Configuration: All validators work
└── Performance: Latency targets met

Unit Tests (existing)
├── EmotionDetectionServiceTests (✓ exists)
├── ToneMatchingServiceTests (✓ exists)
├── ConversationContextAnalyzerTests (✓ exists)
├── SmallTalkServiceTests (✓ exists)
└── ResponseValidationServiceTests (✓ exists)
```

### Data Flow Testing

```
Test Input → EmotionDetectionService
           ↓ EmotionScore
           → ConversationContextAnalyzer
           ↓ ConversationContext
           → ToneMatchingService
           ↓ ToneProfile
           → SmallTalkService
           ↓ SmallTalkResponse
           → [AI Response Generation]
           ↓ Response Text
           → ResponseValidationService
           ↓ ValidationResult
           → Assert: All data correct
```

### New Test Files Structure

```
tests/MessengerWebhook.IntegrationTests/
├── Services/
│   └── Naturalness/
│       ├── NaturalnessPipelineIntegrationTests.cs (NEW)
│       ├── EndToEndScenarioTests.cs (NEW)
│       ├── PerformanceBenchmarkTests.cs (NEW)
│       └── ErrorHandlingIntegrationTests.cs (NEW)
└── Configuration/
    └── NaturalnessConfigurationValidationTests.cs (NEW)
```

## Implementation Steps

### Step 1: Pipeline Integration Tests (1.5 hours)

**1.1 Create NaturalnessPipelineIntegrationTests.cs**

Test full pipeline with real services (no mocks):

```csharp
public class NaturalnessPipelineIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task FullPipeline_HappyPath_AllServicesExecuteCorrectly()
    {
        // Arrange
        var message = "hi sốp ơi, em muốn mua áo thun";
        var history = new List<ConversationMessage>
        {
            new() { Role = "user", Content = message }
        };
        
        // Act - Execute full pipeline
        var emotion = await _emotionService.DetectEmotionWithContextAsync(message, history, default);
        var context = await _contextAnalyzer.AnalyzeWithEmotionAsync(history, new[] { emotion }, default);
        var toneProfile = await _toneService.GenerateToneProfileAsync(emotion, _vipProfile, _customer, 1, default);
        var smallTalk = await _smallTalkService.AnalyzeAsync(message, emotion, toneProfile, context, _vipProfile, true, default);
        
        var validationContext = new ResponseValidationContext
        {
            Response = "Chào chị! Em có áo thun mới về đây ạ 😊",
            ToneProfile = toneProfile,
            ConversationContext = context,
            SmallTalkResponse = smallTalk
        };
        var validation = await _validationService.ValidateAsync(validationContext, default);
        
        // Assert
        Assert.NotNull(emotion);
        Assert.Equal(EmotionType.Neutral, emotion.PrimaryEmotion);
        
        Assert.NotNull(context);
        Assert.Equal(JourneyStage.Browsing, context.CurrentStage);
        
        Assert.NotNull(toneProfile);
        Assert.Equal(ToneLevel.Friendly, toneProfile.Level);
        
        Assert.NotNull(smallTalk);
        Assert.True(smallTalk.IsSmallTalk);
        
        Assert.True(validation.IsValid);
    }
    
    [Fact]
    public async Task Pipeline_WithCache_SecondCallUsesCache()
    {
        // Test emotion/tone caching works
    }
    
    [Fact]
    public async Task Pipeline_DataFlowCorrect_ContextPreservedBetweenServices()
    {
        // Verify data passed correctly between services
    }
}
```

**1.2 Test cache behavior**
- First call: All services execute
- Second call (same input): Emotion/Tone from cache
- Verify cache keys correct
- Verify cache expiration works

**1.3 Test context preservation**
- EmotionScore flows to ContextAnalyzer
- ConversationContext flows to ToneService
- ToneProfile flows to SmallTalkService
- All data available to ValidationService

### Step 2: End-to-End Scenario Tests (1.5 hours)

**2.1 Create EndToEndScenarioTests.cs**

Test complete customer journeys:

```csharp
public class EndToEndScenarioTests : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Scenario_NewCustomerGreeting_FormalIntroduction()
    {
        // Arrange
        var ctx = CreateStateContext(isNewCustomer: true);
        var message = "Xin chào shop";
        
        // Act
        var response = await _handler.HandleAsync(ctx, message);
        
        // Assert
        Assert.Contains("Dạ em chào", response);
        Assert.Contains("Múi Xù", response);
        // Verify formal tone, full catalog intro
    }
    
    [Fact]
    public async Task Scenario_ReturningCustomerGreeting_CasualWelcomeBack()
    {
        // Arrange
        var ctx = CreateStateContext(isReturningCustomer: true);
        var message = "hi sốp";
        
        // Act
        var response = await _handler.HandleAsync(ctx, message);
        
        // Assert
        Assert.DoesNotContain("Múi Xù", response); // No catalog intro
        Assert.Contains("Lâu rồi", response); // Casual greeting
        // Verify friendly tone, no formal markers
    }
    
    [Fact]
    public async Task Scenario_BrowsingStage_ProductQuestions()
    {
        // Customer asks about products casually
        // Verify: Browsing stage detected, friendly tone, product info provided
    }
    
    [Fact]
    public async Task Scenario_ConsideringStage_DetailedInquiry()
    {
        // Customer asks detailed questions (size, price, shipping)
        // Verify: Considering stage detected, more formal tone, detailed answers
    }
    
    [Fact]
    public async Task Scenario_ReadyToBuy_OrderIntent()
    {
        // Customer says "đặt hàng" or "mua"
        // Verify: Ready stage detected, CTA present, order flow initiated
    }
    
    [Fact]
    public async Task Scenario_EmotionalCustomer_FrustratedTone()
    {
        // Customer frustrated: "sao lâu thế", "chờ mãi"
        // Verify: Negative emotion detected, empathetic tone, escalation considered
    }
    
    [Fact]
    public async Task Scenario_SmallTalk_TransitionToBusiness()
    {
        // Customer: "Hôm nay trời đẹp nhỉ"
        // Verify: Small talk detected, brief response, smooth transition to products
    }
}
```

**2.2 Test conversation progression**
- Multi-turn conversations
- Stage transitions (Browsing → Considering → Ready)
- Tone consistency across turns
- Context accumulation

**2.3 Test edge cases**
- Very short messages ("ok", "ừ")
- Very long messages (> 500 chars)
- Mixed language (English product names)
- Emoji-heavy messages

### Step 3: Performance Benchmark Tests (1 hour)

**3.1 Create PerformanceBenchmarkTests.cs**

```csharp
public class PerformanceBenchmarkTests
{
    [Fact]
    public async Task Performance_EmotionDetection_UnderTarget()
    {
        // Arrange
        var message = "hi sốp, em muốn mua áo";
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        for (int i = 0; i < 100; i++)
        {
            await _emotionService.DetectEmotionWithContextAsync(message, _history, default);
        }
        stopwatch.Stop();
        
        // Assert
        var avgMs = stopwatch.ElapsedMilliseconds / 100.0;
        Assert.True(avgMs < 100, $"Emotion detection took {avgMs}ms (target: <100ms)");
    }
    
    [Fact]
    public async Task Performance_FullPipeline_TotalOverheadUnderTarget()
    {
        // Test full pipeline < 100ms total
        // Measure: Emotion + Context + Tone + SmallTalk + Validation
        // Target: P95 < 100ms
    }
    
    [Fact]
    public async Task Performance_CacheHit_SignificantlyFaster()
    {
        // First call: ~100ms
        // Second call (cached): < 10ms
    }
    
    [Fact]
    public async Task Performance_LongHistory_ContextAnalysisScales()
    {
        // Test with 1, 5, 10, 20 turn history
        // Verify: 10 turns < 50ms (target)
        // Verify: 20 turns < 100ms (acceptable)
    }
}
```

**3.2 Measure latency breakdown**
- Emotion detection: individual timing
- Context analysis: individual timing
- Tone matching: individual timing
- Small talk: individual timing
- Validation: individual timing
- Total pipeline: sum vs target

**3.3 Test cache performance**
- Cache hit rate
- Cache miss penalty
- Memory usage (IMemoryCache)

### Step 4: Error Handling Integration Tests (1 hour)

**4.1 Create ErrorHandlingIntegrationTests.cs**

```csharp
public class ErrorHandlingIntegrationTests
{
    [Fact]
    public async Task ErrorHandling_EmotionServiceFails_GracefulDegradation()
    {
        // Arrange: Mock emotion service to throw
        var mockEmotion = new Mock<IEmotionDetectionService>();
        mockEmotion.Setup(x => x.DetectEmotionWithContextAsync(It.IsAny<string>(), It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Emotion service down"));
        
        // Act
        var response = await _handler.HandleAsync(_ctx, "hi sốp");
        
        // Assert
        Assert.NotNull(response); // Should not crash
        Assert.DoesNotContain("error", response.ToLower()); // No error exposed to user
        // Verify: Fallback to neutral emotion, conversation continues
    }
    
    [Fact]
    public async Task ErrorHandling_ToneServiceFails_UsesDefaultTone()
    {
        // Tone service fails → Use default Friendly tone
    }
    
    [Fact]
    public async Task ErrorHandling_ContextAnalyzerFails_AssumesBrowsing()
    {
        // Context analyzer fails → Assume Browsing stage
    }
    
    [Fact]
    public async Task ErrorHandling_SmallTalkServiceFails_SkipsSmallTalk()
    {
        // Small talk fails → Proceed without small talk detection
    }
    
    [Fact]
    public async Task ErrorHandling_ValidationFails_LogsButSends()
    {
        // Validation fails → Log warning, send response anyway (BlockOnErrors=false)
    }
    
    [Fact]
    public async Task ErrorHandling_MultipleServicesFail_StillResponds()
    {
        // Multiple services fail → Use all fallbacks, still generate response
    }
    
    [Fact]
    public async Task ErrorHandling_CascadingFailure_DoesNotOccur()
    {
        // Verify: One service failure doesn't cause others to fail
    }
}
```

**4.2 Test null safety**
- Null emotion score
- Null tone profile
- Null conversation context
- Null small talk response
- Null validation result

**4.3 Test timeout handling**
- Service takes too long
- Cancellation token respected
- Timeout doesn't crash pipeline

### Step 5: Configuration Validation Tests (30 min)

**5.1 Create NaturalnessConfigurationValidationTests.cs**

```csharp
public class NaturalnessConfigurationValidationTests
{
    [Fact]
    public void Configuration_EmotionOptions_ValidatesCorrectly()
    {
        // Valid config: passes
        var validOptions = new EmotionDetectionOptions
        {
            EnableEmotionDetection = true,
            ConfidenceThreshold = 0.6,
            CacheDurationMinutes = 5
        };
        var validator = new EmotionDetectionOptionsValidator();
        var result = validator.Validate(validOptions);
        Assert.True(result.IsValid);
        
        // Invalid config: fails
        var invalidOptions = new EmotionDetectionOptions
        {
            ConfidenceThreshold = 1.5 // > 1.0
        };
        result = validator.Validate(invalidOptions);
        Assert.False(result.IsValid);
    }
    
    [Fact]
    public void Configuration_ToneOptions_ValidatesCorrectly()
    {
        // Test ToneMatchingOptionsValidator
    }
    
    [Fact]
    public void Configuration_ConversationOptions_ValidatesCorrectly()
    {
        // Test ConversationContextOptionsValidator
    }
    
    [Fact]
    public void Configuration_SmallTalkOptions_ValidatesCorrectly()
    {
        // Test SmallTalkOptionsValidator
    }
    
    [Fact]
    public void Configuration_ValidationOptions_ValidatesCorrectly()
    {
        // Test ResponseValidationOptionsValidator
    }
    
    [Fact]
    public void Configuration_InvalidConfig_ApplicationStartupFails()
    {
        // Test that invalid config prevents app startup
        // Use CustomWebApplicationFactory with invalid appsettings
    }
}
```

**5.2 Test default values**
- All Options classes have sensible defaults
- App works with minimal config
- Feature flags default to safe values

**5.3 Test feature flags**
- EnableEmotionDetection = false → skips emotion
- EnableToneMatching = false → uses default tone
- EnableValidation = false → skips validation
- All combinations work correctly

### Step 6: Backward Compatibility Tests (30 min)

**6.1 Test existing flows unaffected**

```csharp
[Fact]
public async Task BackwardCompatibility_ExistingVipFlow_StillWorks()
{
    // VIP customer flow unchanged
    // Verify: VIP greeting, formal tone, catalog intro
}

[Fact]
public async Task BackwardCompatibility_ExistingErrorHandling_StillWorks()
{
    // Error state transitions unchanged
}

[Fact]
public async Task BackwardCompatibility_ExistingCTA_StillWorks()
{
    // CTA generation unchanged
}
```

**6.2 Test data migration**
- Existing ConversationSession data compatible
- Existing Customer data compatible
- No breaking changes to database schema

### Step 7: Documentation & Cleanup (30 min)

**7.1 Update test documentation**
- Add README to `tests/MessengerWebhook.IntegrationTests/Services/Naturalness/`
- Document test scenarios
- Document performance targets
- Document how to run tests

**7.2 Update appsettings.json**
- Add all new Options sections
- Document each setting
- Provide sensible defaults

**7.3 Update CLAUDE.md**
- Document new services
- Document testing approach
- Document performance targets

## Todo List

### Integration Tests
- [ ] Create NaturalnessPipelineIntegrationTests.cs
- [ ] Test full pipeline happy path
- [ ] Test cache behavior (hit/miss)
- [ ] Test data flow between services
- [ ] Test context preservation

### E2E Scenario Tests
- [ ] Create EndToEndScenarioTests.cs
- [ ] Test new customer greeting
- [ ] Test returning customer greeting
- [ ] Test browsing stage scenario
- [ ] Test considering stage scenario
- [ ] Test ready to buy scenario
- [ ] Test emotional customer scenario
- [ ] Test small talk scenario
- [ ] Test conversation progression
- [ ] Test edge cases (short/long/mixed messages)

### Performance Tests
- [ ] Create PerformanceBenchmarkTests.cs
- [ ] Benchmark emotion detection (< 100ms)
- [ ] Benchmark context analysis (< 50ms for 10 turns)
- [ ] Benchmark tone matching (< 50ms)
- [ ] Benchmark small talk (< 30ms)
- [ ] Benchmark validation (< 50ms)
- [ ] Benchmark full pipeline (< 100ms total)
- [ ] Test cache performance
- [ ] Test long history scaling

### Error Handling Tests
- [ ] Create ErrorHandlingIntegrationTests.cs
- [ ] Test emotion service failure
- [ ] Test tone service failure
- [ ] Test context analyzer failure
- [ ] Test small talk service failure
- [ ] Test validation service failure
- [ ] Test multiple service failures
- [ ] Test cascading failure prevention
- [ ] Test null safety
- [ ] Test timeout handling

### Configuration Tests
- [ ] Create NaturalnessConfigurationValidationTests.cs
- [ ] Test EmotionDetectionOptions validation
- [ ] Test ToneMatchingOptions validation
- [ ] Test ConversationContextOptions validation
- [ ] Test SmallTalkOptions validation
- [ ] Test ResponseValidationOptions validation
- [ ] Test invalid config prevents startup
- [ ] Test default values work
- [ ] Test feature flags

### Backward Compatibility
- [ ] Test existing VIP flow unchanged
- [ ] Test existing error handling unchanged
- [ ] Test existing CTA unchanged
- [ ] Test data migration compatibility

### Documentation
- [ ] Add test README
- [ ] Update appsettings.json with all Options
- [ ] Update CLAUDE.md with testing info
- [ ] Document performance targets
- [ ] Document test scenarios

### Final Verification
- [ ] Run all tests and verify pass
- [ ] Verify test coverage ≥ 85%
- [ ] Verify performance targets met
- [ ] Verify no regressions in existing tests
- [ ] Code review with `code-reviewer` agent

## Success Criteria

- ✅ All integration tests passing (10+ scenarios)
- ✅ All E2E scenarios passing (5+ journeys)
- ✅ Performance targets met:
  - Emotion detection: < 100ms
  - Context analysis: < 50ms (10 turns)
  - Tone matching: < 50ms
  - Small talk: < 30ms
  - Validation: < 50ms
  - Total overhead: < 100ms (P95)
- ✅ Error handling: Graceful degradation for all services
- ✅ Configuration: All validators working
- ✅ Test coverage: ≥ 85% for new services
- ✅ Backward compatibility: No regressions
- ✅ Documentation: Complete and accurate

## Risk Assessment

### High Risk
1. **Performance Regression**
   - Risk: Pipeline adds > 100ms latency
   - Mitigation: Benchmark each service, optimize hot paths, use caching
   - Contingency: Feature flags to disable slow services

2. **Integration Bugs**
   - Risk: Services don't work together correctly
   - Mitigation: Comprehensive integration tests, data flow validation
   - Contingency: Rollback to previous version, fix bugs incrementally

### Medium Risk
3. **Cache Issues**
   - Risk: Cache invalidation bugs, memory leaks
   - Mitigation: Test cache behavior thoroughly, monitor memory usage
   - Contingency: Disable caching if issues occur

4. **Error Handling Gaps**
   - Risk: Unhandled edge cases crash pipeline
   - Mitigation: Test all failure modes, add null checks
   - Contingency: Add more try-catch blocks, improve logging

### Low Risk
5. **Configuration Complexity**
   - Risk: Too many config options confuse users
   - Mitigation: Sensible defaults, clear documentation
   - Contingency: Simplify config, remove unused options

## Testing Strategy

### Test Levels
1. **Unit Tests** (existing): Test individual services in isolation
2. **Integration Tests** (new): Test services working together
3. **E2E Tests** (new): Test complete customer journeys
4. **Performance Tests** (new): Verify latency targets
5. **Error Tests** (new): Verify graceful degradation

### Test Data
- Use realistic Vietnamese messages
- Cover all customer tiers (Standard, Returning, VIP)
- Cover all journey stages (Browsing, Considering, Ready)
- Cover all emotion types (Positive, Neutral, Negative, Frustrated)
- Cover all tone levels (Formal, Friendly, Casual)

### Test Execution
- Run unit tests on every commit
- Run integration tests before merge
- Run performance tests weekly
- Run E2E tests before release

### Test Maintenance
- Update tests when requirements change
- Remove obsolete tests
- Keep test data realistic
- Monitor test execution time

## Performance Targets

| Service | Target | Measurement |
|---------|--------|-------------|
| Emotion Detection | < 100ms | P95 latency |
| Context Analysis | < 50ms | 10-turn history |
| Tone Matching | < 50ms | P95 latency |
| Small Talk | < 30ms | P95 latency |
| Validation | < 50ms | P95 latency |
| **Total Pipeline** | **< 100ms** | **P95 end-to-end** |
| Cache Hit | < 10ms | Emotion/Tone cached |

## Next Steps

After Phase 6:
1. Review test results with team
2. Fix any failing tests or performance issues
3. Update documentation based on test findings
4. Move to Phase 7: A/B Testing & Metrics
5. Deploy to staging for manual testing
6. Prepare production rollout plan

## Unresolved Questions

1. Should we add load testing (concurrent requests)?
2. Do we need chaos engineering tests (random service failures)?
3. Should we test with real Facebook Messenger API?
4. Do we need visual regression tests for response format?
5. Should we add mutation testing for test quality?
