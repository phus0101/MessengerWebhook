# Phase 5: Configuration and Dependency Injection

**Priority:** P1  
**Status:** pending  
**Effort:** 2h  
**Dependencies:** Phase 1, Phase 2, Phase 3, Phase 4

## Context Links

- Research report: `plans/reports/researcher-260503-1142-ai-intent-classification.md` (lines 262-270)
- Existing config: `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- Existing DI: `src/MessengerWebhook/Program.cs`
- appsettings: `src/MessengerWebhook/appsettings.json`

## Overview

Create configuration options class and register all sub-intent services in DI container. Configure thresholds, timeouts, circuit breaker settings.

## Key Insights

- Pattern from `PolicyGuardOptions` - validation attributes, IOptions<T> pattern
- Register services in `Program.cs` following existing patterns
- Add `SubIntent` section to `appsettings.json`
- Use `IValidateOptions<T>` for startup validation
- HttpClient for `GeminiSubIntentClassifier` needs base URL configuration

## Requirements

### Functional
- Create `SubIntentOptions` with all configurable thresholds
- Add validation for confidence ranges (0-1)
- Register services in DI container (scoped lifetime)
- Configure HttpClient for Gemini API
- Add `SubIntent` section to appsettings.json
- Support environment-specific overrides

### Non-Functional
- Fail fast on invalid configuration (startup validation)
- Type-safe configuration (strongly typed options)
- Hot reload support (IOptionsMonitor)
- Clear validation error messages

## Architecture

```
Configuration/
└── SubIntentOptions.cs
    ├── KeywordHighConfidenceThreshold (0.9)
    ├── MinConfidence (0.7)
    ├── ClassifierTimeoutMs (500)
    ├── CircuitBreakerThreshold (5)
    └── CircuitBreakerCooldownMinutes (10)

Program.cs
├── services.Configure<SubIntentOptions>()
├── services.AddSingleton<KeywordSubIntentDetector>()
├── services.AddScoped<GeminiSubIntentClassifier>()
├── services.AddScoped<HybridSubIntentClassifier>()
└── services.AddScoped<ISubIntentClassifier, HybridSubIntentClassifier>()

appsettings.json
└── SubIntent: { ... }
```

## Related Code Files

**To create:**
- `src/MessengerWebhook/Configuration/SubIntentOptions.cs`
- `src/MessengerWebhook/Configuration/ValidateSubIntentOptions.cs`

**To modify:**
- `src/MessengerWebhook/Program.cs` (add DI registrations)
- `src/MessengerWebhook/appsettings.json` (add SubIntent section)

**Reference:**
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs` (pattern)
- `src/MessengerWebhook/Configuration/ValidatePolicyGuardOptions.cs` (validation pattern)

## Implementation Steps

### 1. Create SubIntentOptions class (30min)

```csharp
using System.ComponentModel.DataAnnotations;

namespace MessengerWebhook.Configuration;

/// <summary>
/// Configuration options for sub-intent classification
/// </summary>
public sealed class SubIntentOptions
{
    /// <summary>
    /// Keyword confidence threshold for immediate acceptance (skip AI)
    /// Default: 0.9 (high confidence)
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal KeywordHighConfidenceThreshold { get; set; } = 0.9m;

    /// <summary>
    /// Minimum confidence threshold for AI classifier results
    /// Default: 0.7 (medium-high confidence)
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal MinConfidence { get; set; } = 0.7m;

    /// <summary>
    /// Timeout for AI classifier in milliseconds
    /// Default: 500ms (fast fallback)
    /// </summary>
    [Range(100, 5000)]
    public int ClassifierTimeoutMs { get; set; } = 500;

    /// <summary>
    /// Number of consecutive AI failures before circuit breaker opens
    /// Default: 5 failures
    /// </summary>
    [Range(1, 100)]
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker cooldown period in minutes
    /// Default: 10 minutes
    /// </summary>
    [Range(1, 60)]
    public int CircuitBreakerCooldownMinutes { get; set; } = 10;

    /// <summary>
    /// Enable AI classifier (if false, keyword-only mode)
    /// Default: true
    /// </summary>
    public bool EnableAiClassifier { get; set; } = true;

    /// <summary>
    /// Enable circuit breaker pattern
    /// Default: true
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;
}
```

### 2. Create validation class (20min)

```csharp
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration;

/// <summary>
/// Validates SubIntentOptions at startup
/// </summary>
public sealed class ValidateSubIntentOptions : IValidateOptions<SubIntentOptions>
{
    public ValidateOptionsResult Validate(string? name, SubIntentOptions options)
    {
        var errors = new List<string>();

        if (options.KeywordHighConfidenceThreshold < 0 || options.KeywordHighConfidenceThreshold > 1)
        {
            errors.Add($"{nameof(options.KeywordHighConfidenceThreshold)} must be between 0 and 1");
        }

        if (options.MinConfidence < 0 || options.MinConfidence > 1)
        {
            errors.Add($"{nameof(options.MinConfidence)} must be between 0 and 1");
        }

        if (options.KeywordHighConfidenceThreshold <= options.MinConfidence)
        {
            errors.Add($"{nameof(options.KeywordHighConfidenceThreshold)} must be greater than {nameof(options.MinConfidence)}");
        }

        if (options.ClassifierTimeoutMs < 100 || options.ClassifierTimeoutMs > 5000)
        {
            errors.Add($"{nameof(options.ClassifierTimeoutMs)} must be between 100 and 5000");
        }

        if (options.CircuitBreakerThreshold < 1)
        {
            errors.Add($"{nameof(options.CircuitBreakerThreshold)} must be at least 1");
        }

        if (options.CircuitBreakerCooldownMinutes < 1)
        {
            errors.Add($"{nameof(options.CircuitBreakerCooldownMinutes)} must be at least 1");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

### 3. Register services in Program.cs (40min)

```csharp
// Add after existing service registrations (around line 100-150)

// Sub-intent classification configuration
builder.Services.Configure<SubIntentOptions>(builder.Configuration.GetSection("SubIntent"));
builder.Services.AddSingleton<IValidateOptions<SubIntentOptions>, ValidateSubIntentOptions>();

// Sub-intent classification services
builder.Services.AddSingleton<KeywordSubIntentDetector>();

builder.Services.AddHttpClient<GeminiSubIntentClassifier>(client =>
{
    var geminiOptions = builder.Configuration.GetSection("Gemini").Get<GeminiOptions>();
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(geminiOptions?.TimeoutSeconds ?? 60);
});

builder.Services.AddScoped<HybridSubIntentClassifier>();

// Register ISubIntentClassifier interface
builder.Services.AddScoped<ISubIntentClassifier, HybridSubIntentClassifier>();
```

### 4. Add configuration to appsettings.json (30min)

```json
{
  "SubIntent": {
    "KeywordHighConfidenceThreshold": 0.9,
    "MinConfidence": 0.7,
    "ClassifierTimeoutMs": 500,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerCooldownMinutes": 10,
    "EnableAiClassifier": true,
    "EnableCircuitBreaker": true
  }
}
```

## Todo List

- [ ] Create `SubIntentOptions.cs` with all configuration properties
- [ ] Add validation attributes ([Range], etc.)
- [ ] Create `ValidateSubIntentOptions.cs` for startup validation
- [ ] Add cross-field validation (KeywordThreshold > MinConfidence)
- [ ] Register services in `Program.cs`
- [ ] Configure HttpClient for `GeminiSubIntentClassifier`
- [ ] Add `SubIntent` section to `appsettings.json`
- [ ] Add XML documentation to all properties
- [ ] Compile and verify no errors
- [ ] Test startup validation (invalid config should fail fast)

## Success Criteria

- [ ] `SubIntentOptions` has all required properties with defaults
- [ ] Validation fails on invalid confidence ranges (0-1)
- [ ] Validation fails if KeywordThreshold ≤ MinConfidence
- [ ] All services registered in DI container
- [ ] HttpClient configured with correct base URL and timeout
- [ ] Configuration loads from appsettings.json
- [ ] Startup validation runs and fails fast on invalid config
- [ ] Services resolve correctly (no DI errors)

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Missing DI registration | Low | High | Compile-time check, integration test |
| Invalid default values | Low | Medium | Validation at startup, unit tests |
| HttpClient misconfiguration | Low | High | Integration test with real API |
| Configuration not loaded | Low | High | Startup validation, fail fast |

## Security Considerations

- No secrets in `SubIntentOptions` (API keys in `GeminiOptions`)
- Validation prevents resource exhaustion (timeout limits)
- Circuit breaker prevents cascading failures

## Next Steps

**Blocks:** Phase 6 (Integration with State Handlers)

**After completion:**
1. Verify services resolve correctly (dotnet run)
2. Test configuration hot reload (change appsettings, verify reload)
3. Proceed to Phase 6: Integration with state handlers
