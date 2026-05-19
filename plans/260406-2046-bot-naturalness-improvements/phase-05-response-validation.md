# Phase 5: Response Validation

**Status:** pending  
**Priority:** medium  
**Timeline:** Day 7  
**Dependencies:** Phase 1-4 (Emotion, Tone, Context, SmallTalk)

## Context Links

- Plan Overview: `plan.md`
- Previous Phase: `phase-04-small-talk-natural-flow.md`
- Emotion Detection: `src/MessengerWebhook/Services/Emotion/EmotionDetectionService.cs`
- Tone Matching: `src/MessengerWebhook/Services/Tone/ToneMatchingService.cs`
- Context Analyzer: `src/MessengerWebhook/Services/Conversation/ConversationContextAnalyzer.cs`
- Small Talk: `src/MessengerWebhook/Services/SmallTalk/SmallTalkService.cs`

## Overview

Implement response validation service để verify bot responses match expected tone, context, và quality standards trước khi gửi cho customer. Service validates tone consistency, context appropriateness, Vietnamese language quality, và response structure.

**Priority:** Medium - Quality assurance layer  
**Current Status:** Not started  
**Estimated Effort:** 2-3 hours

## Key Insights

From completed phases:
- Phase 2 provides ToneProfile với expected tone level
- Phase 3 provides ConversationContext với journey stage
- Phase 4 provides SmallTalkResponse với transition readiness
- Need to validate AI-generated responses match these expectations

From existing codebase:
- AI responses generated in SalesStateHandlerBase
- No validation layer currently exists
- Responses sent directly to customer without quality checks

Vietnamese quality checks needed:
- Proper pronoun usage (anh/chị/em/bạn)
- Formal vs casual language consistency
- No mixed English-Vietnamese (unless product names)
- Appropriate emoji usage

## Requirements

### Functional Requirements

1. **Tone Consistency Validation**
   - Verify response matches expected tone level (Formal/Friendly/Casual)
   - Check pronoun usage matches ToneProfile
   - Validate formality markers (dạ, ạ, vâng)

2. **Context Appropriateness**
   - Verify response matches journey stage (Browsing/Considering/Ready)
   - Check transition readiness alignment
   - Validate small talk vs business content ratio

3. **Vietnamese Language Quality**
   - Check for proper diacritics
   - Validate sentence structure
   - Detect mixed language issues
   - Verify emoji appropriateness

4. **Response Structure**
   - Length validation (not too short/long)
   - Paragraph structure
   - Question presence when needed
   - Call-to-action appropriateness

### Non-Functional Requirements

- Performance: < 50ms validation overhead
- Accuracy: 90%+ validation correctness
- No false positives: Don't block valid responses
- Graceful degradation: Log issues but don't block on minor problems

## Architecture

### Component Design

```
ResponseValidationService
├── Input: Response text, ToneProfile, ConversationContext, SmallTalkResponse
├── Validators:
│   ├── ToneConsistencyValidator
│   ├── ContextAppropriatenessValidator
│   ├── VietnameseQualityValidator
│   └── StructureValidator
└── Output: ValidationResult (IsValid, Issues[], Warnings[])
```

### Data Flow

```
AI Generated Response
→ ResponseValidationService
  → ToneConsistencyValidator (check tone match)
  → ContextAppropriatenessValidator (check context fit)
  → VietnameseQualityValidator (check language quality)
  → StructureValidator (check format)
→ ValidationResult
→ If valid: Send to customer
→ If invalid: Log + fallback response
```

### New Files Structure

```
src/MessengerWebhook/Services/ResponseValidation/
├── IResponseValidationService.cs
├── ResponseValidationService.cs
├── Validators/
│   ├── ToneConsistencyValidator.cs
│   ├── ContextAppropriatenessValidator.cs
│   ├── VietnameseQualityValidator.cs
│   └── StructureValidator.cs
├── Models/
│   ├── ValidationResult.cs
│   ├── ValidationIssue.cs
│   ├── ValidationSeverity.cs
│   └── ResponseValidationContext.cs
└── Configuration/
    └── ResponseValidationOptions.cs
```

## Implementation Steps

### Step 1: Create Models (20 min)

**1.1 ValidationSeverity enum**
```csharp
public enum ValidationSeverity
{
    Info,       // Informational, no action needed
    Warning,    // Minor issue, log but allow
    Error,      // Major issue, should block
    Critical    // Critical issue, must block
}
```

**1.2 ValidationIssue model**
```csharp
public class ValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SuggestedFix { get; set; }
}
```

**1.3 ValidationResult model**
```csharp
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public List<ValidationIssue> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

**1.4 ResponseValidationContext model**
```csharp
public class ResponseValidationContext
{
    public string Response { get; set; } = string.Empty;
    public ToneProfile ToneProfile { get; set; } = null!;
    public ConversationContext ConversationContext { get; set; } = null!;
    public SmallTalkResponse? SmallTalkResponse { get; set; }
}
```

### Step 2: Create Configuration (10 min)

```csharp
public class ResponseValidationOptions
{
    public bool EnableValidation { get; set; } = true;
    public bool EnableToneValidation { get; set; } = true;
    public bool EnableContextValidation { get; set; } = true;
    public bool EnableLanguageValidation { get; set; } = true;
    public bool EnableStructureValidation { get; set; } = true;
    public int MinResponseLength { get; set; } = 10;
    public int MaxResponseLength { get; set; } = 500;
    public bool BlockOnErrors { get; set; } = false; // Log only by default
}
```

### Step 3: Implement Validators (1 hour)

**3.1 ToneConsistencyValidator**
```csharp
public class ToneConsistencyValidator
{
    public List<ValidationIssue> Validate(string response, ToneProfile toneProfile)
    {
        var issues = new List<ValidationIssue>();
        
        // Check pronoun usage
        var expectedPronoun = toneProfile.PronounText;
        if (!response.Contains(expectedPronoun))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Tone",
                Message = $"Expected pronoun '{expectedPronoun}' not found in response"
            });
        }
        
        // Check formality markers for Formal tone
        if (toneProfile.Level == ToneLevel.Formal)
        {
            if (!response.Contains("dạ") && !response.Contains("ạ"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "Tone",
                    Message = "Formal tone expected but no formality markers (dạ/ạ) found"
                });
            }
        }
        
        return issues;
    }
}
```

**3.2 VietnameseQualityValidator**
```csharp
public class VietnameseQualityValidator
{
    private static readonly HashSet<string> CommonMixedLanguagePatterns = new()
    {
        "hi bạn", "hello shop", "thank you", "sorry"
    };
    
    public List<ValidationIssue> Validate(string response)
    {
        var issues = new List<ValidationIssue>();
        
        // Check for mixed language (except product names)
        var lowerResponse = response.ToLower();
        foreach (var pattern in CommonMixedLanguagePatterns)
        {
            if (lowerResponse.Contains(pattern))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "Language",
                    Message = $"Mixed language detected: '{pattern}'"
                });
            }
        }
        
        // Check for excessive emoji
        var emojiCount = response.Count(c => c >= 0x1F600 && c <= 0x1F64F);
        if (emojiCount > 3)
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Language",
                Message = $"Excessive emoji usage: {emojiCount} emojis"
            });
        }
        
        return issues;
    }
}
```

### Step 4: Main Service (45 min)

```csharp
public class ResponseValidationService : IResponseValidationService
{
    private readonly ToneConsistencyValidator _toneValidator;
    private readonly ContextAppropriatenessValidator _contextValidator;
    private readonly VietnameseQualityValidator _languageValidator;
    private readonly StructureValidator _structureValidator;
    private readonly ILogger<ResponseValidationService> _logger;
    private readonly ResponseValidationOptions _options;
    
    public async Task<ValidationResult> ValidateAsync(
        ResponseValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableValidation)
            return new ValidationResult { IsValid = true };
        
        var allIssues = new List<ValidationIssue>();
        
        // Run validators
        if (_options.EnableToneValidation)
            allIssues.AddRange(_toneValidator.Validate(context.Response, context.ToneProfile));
        
        if (_options.EnableContextValidation)
            allIssues.AddRange(_contextValidator.Validate(context.Response, context.ConversationContext));
        
        if (_options.EnableLanguageValidation)
            allIssues.AddRange(_languageValidator.Validate(context.Response));
        
        if (_options.EnableStructureValidation)
            allIssues.AddRange(_structureValidator.Validate(context.Response, _options));
        
        // Categorize by severity
        var errors = allIssues.Where(i => i.Severity >= ValidationSeverity.Error).ToList();
        var warnings = allIssues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
        
        var isValid = !errors.Any() || !_options.BlockOnErrors;
        
        if (!isValid)
        {
            _logger.LogWarning(
                "Response validation failed with {ErrorCount} errors",
                errors.Count);
        }
        
        return new ValidationResult
        {
            IsValid = isValid,
            Issues = errors,
            Warnings = warnings
        };
    }
}
```

### Step 5: Integration (15 min)

Update `SalesStateHandlerBase.cs`:
```csharp
// After AI generates response
var validationContext = new ResponseValidationContext
{
    Response = aiResponse,
    ToneProfile = toneProfile,
    ConversationContext = conversationContext,
    SmallTalkResponse = smallTalkResponse
};

var validationResult = await _responseValidationService
    .ValidateAsync(validationContext, cancellationToken);

if (!validationResult.IsValid)
{
    Logger.LogWarning(
        "Response validation failed: {Issues}",
        string.Join(", ", validationResult.Issues.Select(i => i.Message)));
    
    // Use fallback response or retry
}
```

### Step 6: Unit Tests (30 min)

Test cases:
1. Valid response passes all validators
2. Missing pronoun triggers warning
3. Formal tone without dạ/ạ triggers warning
4. Excessive emoji triggers warning
5. Too short response triggers error
6. Too long response triggers error
7. Mixed language detected
8. Validation disabled returns valid
9. BlockOnErrors=false allows errors
10. Multiple issues aggregated correctly

## Todo List

- [ ] Create ValidationSeverity enum
- [ ] Create ValidationIssue model
- [ ] Create ValidationResult model
- [ ] Create ResponseValidationContext model
- [ ] Create ResponseValidationOptions
- [ ] Implement ToneConsistencyValidator
- [ ] Implement ContextAppropriatenessValidator
- [ ] Implement VietnameseQualityValidator
- [ ] Implement StructureValidator
- [ ] Create IResponseValidationService interface
- [ ] Implement ResponseValidationService
- [ ] Register services in Program.cs
- [ ] Integrate with SalesStateHandlerBase
- [ ] Write unit tests (10+ cases)
- [ ] Run tests and verify pass
- [ ] Update appsettings.json

## Success Criteria

- ✅ All validators implemented
- ✅ Tone consistency checked
- ✅ Vietnamese quality validated
- ✅ Response structure validated
- ✅ Integration complete
- ✅ Performance < 50ms
- ✅ Test coverage ≥ 85%
- ✅ All tests passing

## Risk Assessment

### Medium Risk
1. **False Positives**
   - Risk: Blocking valid responses
   - Mitigation: Default to log-only mode (BlockOnErrors=false)
   - Contingency: Tune validation rules from production data

2. **Performance Impact**
   - Risk: Validation adds latency
   - Mitigation: Simple rule-based checks, no ML
   - Contingency: Disable specific validators if needed

### Low Risk
3. **Vietnamese Detection Accuracy**
   - Risk: Missing language quality issues
   - Mitigation: Start with common patterns
   - Contingency: Expand rules incrementally

## Testing Strategy

- Unit tests for each validator
- Integration tests with real responses
- Performance tests (< 50ms target)
- False positive rate monitoring

## Next Steps

After Phase 5:
1. Monitor validation metrics in production
2. Tune validation rules based on false positive rate
3. Move to Phase 6: Integration & Testing
4. Consider ML-based validation in future
