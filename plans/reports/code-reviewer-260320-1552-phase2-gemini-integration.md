# Code Review: Phase 2 - Gemini Integration

**Reviewer**: code-reviewer
**Date**: 2026-03-20
**Commit**: ca868e9 (fix: address high priority database issues from code review)
**Plan**: D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260320-1042-gemini-sales-chatbot/phase-02-gemini-integration.md

---

## Scope

**Files Reviewed**: 13 files (11 new, 2 modified)
- Configuration: `GeminiOptions.cs`
- Services: `IGeminiService.cs`, `GeminiService.cs`
- Handlers: `GeminiAuthHandler.cs`, `GeminiRetryHandler.cs`
- Strategies: `IModelSelectionStrategy.cs`, `HybridModelSelectionStrategy.cs`
- Models: `GeminiRequest.cs`, `GeminiResponse.cs`, `ConversationMessage.cs`, `GeminiModelType.cs`
- Integration: `Program.cs`, `appsettings.json`

**LOC**: 326 lines total
**Build Status**: ✅ Success (1 warning)
**Packages**: Polly 8.0.0, Mscc.GenerativeAI 3.1.0

---

## Overall Assessment

**Quality**: Good (7.5/10)

Implementation follows ASP.NET Core best practices with proper DI, HttpClient factory pattern, and delegating handlers. Architecture is clean with separation of concerns. However, several critical security and reliability issues need addressing before production use.

---

## Critical Issues

### 1. **API Key Exposed in appsettings.json** 🔴
**File**: `appsettings.json:41`
```json
"Gemini": {
  "ApiKey": "",  // Empty but structure exposed
```

**Problem**: API key structure visible in committed config file. Plan specifies User Secrets but implementation doesn't validate this.

**Impact**: Security vulnerability if developers accidentally commit keys.

**Fix**: Add startup validation in `Program.cs`:
```csharp
var geminiOpts = app.Services.GetRequiredService<IOptions<GeminiOptions>>().Value;
if (string.IsNullOrWhiteSpace(geminiOpts.ApiKey))
    throw new InvalidOperationException("Gemini:ApiKey is required. Configure via User Secrets or environment variables.");
```

### 2. **Sensitive Data in appsettings.json** 🔴
**File**: `appsettings.json:35-36`
```json
"AppSecret": "b076a7769e709a05324486a42bd53aff",
"PageAccessToken": "EAAVisIh2O94BQ7ZCXADAxchsxR3iBhSKvR7igIGJqqfr9VeCnDsoQYhFJz1slePWA3ZC838OajtYE6Rr8jyiHvZBS78jFhe2WZBZCDacWWYwr7pzxGcebzeVZBZCXwZCsUMpbZAxjy2IREmeN8qx7XkWliCmjI0yShGopZBoPv4d1fY5zoEoSgb3rZBBuFCX0az8Fh3i6W4AgZDZD"
```

**Problem**: Facebook credentials committed to repository (not Gemini-specific but discovered during review).

**Impact**: CRITICAL - Active credentials exposed in version control.

**Fix**:
1. Immediately revoke these tokens in Facebook Developer Console
2. Move to User Secrets
3. Add `.gitignore` entry for `appsettings.Development.json`

### 3. **Missing Null Safety in Response Parsing** 🔴
**File**: `GeminiService.cs:68-69`
```csharp
var responseText = result?.Candidates?[0]?.Content?.Parts?[0]?.Text
    ?? throw new InvalidOperationException("Invalid response format");
```

**Problem**: Throws generic exception without logging response details. No handling for empty candidates array.

**Impact**: Difficult to debug API errors, poor error messages to users.

**Fix**:
```csharp
if (result?.Candidates == null || result.Candidates.Length == 0)
{
    _logger.LogError("Gemini returned no candidates. Response: {Response}",
        await response.Content.ReadAsStringAsync(cancellationToken));
    throw new InvalidOperationException("Gemini API returned no response candidates");
}

var candidate = result.Candidates[0];
if (candidate?.Content?.Parts == null || candidate.Content.Parts.Length == 0)
{
    _logger.LogError("Gemini candidate has no content parts. FinishReason: {Reason}",
        candidate?.FinishReason);
    throw new InvalidOperationException($"Gemini response incomplete. Reason: {candidate?.FinishReason}");
}

var responseText = candidate.Content.Parts[0].Text;
if (string.IsNullOrWhiteSpace(responseText))
{
    throw new InvalidOperationException("Gemini returned empty response text");
}
```

---

## High Priority

### 4. **CancellationToken Not Properly Propagated** 🟠
**File**: `GeminiService.cs:78` (Build Warning CS8425)
```csharp
public async IAsyncEnumerable<string> StreamMessageAsync(
    string userId,
    string message,
    List<ConversationMessage> history,
    GeminiModelType? modelOverride = null,
    CancellationToken cancellationToken = default)
```

**Problem**: Compiler warning - CancellationToken parameter won't be used by async enumerable consumers.

**Impact**: Streaming operations can't be cancelled properly.

**Fix**:
```csharp
public async IAsyncEnumerable<string> StreamMessageAsync(
    string userId,
    string message,
    List<ConversationMessage> history,
    GeminiModelType? modelOverride = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

Add using: `using System.Runtime.CompilerServices;`

### 5. **Streaming Not Implemented** 🟠
**File**: `GeminiService.cs:86-88`
```csharp
// Streaming implementation placeholder
// For now, return the full response as a single chunk
var response = await SendMessageAsync(userId, message, history, modelOverride, cancellationToken);
yield return response;
```

**Problem**: Plan requires streaming for <1s first token, but implementation is placeholder.

**Impact**: Fails success criteria, poor UX for long responses.

**Recommendation**: Either implement real streaming or remove method until Phase 3.

### 6. **No Rate Limiting Implementation** 🟠
**File**: `GeminiOptions.cs:14`, `GeminiRetryHandler.cs`

**Problem**: RateLimitOptions defined but never used. No rate limiting logic implemented.

**Impact**: Can exceed Gemini API limits (60 RPM), causing 429 errors.

**Fix**: Implement rate limiter using `System.Threading.RateLimiting`:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("gemini", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60;
        opt.QueueLimit = 10;
    });
});
```

### 7. **HttpClient Timeout Mismatch** 🟠
**File**: `Program.cs:67`, `GeminiOptions.cs:13`
```csharp
client.Timeout = TimeSpan.FromSeconds(60);  // Hardcoded
// vs
public int TimeoutSeconds { get; set; } = 60;  // Config
```

**Problem**: Timeout hardcoded in Program.cs instead of using config value.

**Impact**: Configuration changes ignored.

**Fix**:
```csharp
builder.Services.AddHttpClient<IGeminiService, GeminiService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
```

### 8. **Missing Input Validation** 🟠
**File**: `GeminiService.cs:28-33`

**Problem**: No validation of input parameters (userId, message, history).

**Impact**: Can send invalid requests to API, waste tokens, cause errors.

**Fix**:
```csharp
public async Task<string> SendMessageAsync(
    string userId,
    string message,
    List<ConversationMessage> history,
    GeminiModelType? modelOverride = null,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
    ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
    ArgumentNullException.ThrowIfNull(history, nameof(history));

    if (message.Length > 10000)
    {
        _logger.LogWarning("Message too long: {Length} chars for user {UserId}",
            message.Length, userId);
        throw new ArgumentException("Message exceeds maximum length of 10000 characters", nameof(message));
    }

    // ... rest of implementation
}
```

---

## Medium Priority

### 9. **History Truncation Without Warning** 🟡
**File**: `GeminiService.cs:119`
```csharp
foreach (var msg in history.TakeLast(10))
```

**Problem**: Silently truncates history to last 10 messages. No logging or user notification.

**Impact**: Context loss in long conversations, users unaware of limitation.

**Fix**:
```csharp
var historyToSend = history.TakeLast(10).ToList();
if (history.Count > 10)
{
    _logger.LogInformation(
        "Truncating conversation history from {Total} to {Kept} messages for user {UserId}",
        history.Count, historyToSend.Count, userId);
}

foreach (var msg in historyToSend)
{
    // ...
}
```

### 10. **Model Names Hardcoded Incorrectly** 🟡
**File**: `GeminiOptions.cs:8-9`, Plan specifies `gemini-3.1-pro`
```csharp
public string ProModel { get; set; } = "gemini-2.0-flash-exp";
public string FlashLiteModel { get; set; } = "gemini-2.0-flash-exp";
```

**Problem**:
1. Both models default to same value (2.0-flash-exp)
2. Plan specifies 3.1 models but implementation uses 2.0
3. Using experimental model as default

**Impact**: No model differentiation, unexpected behavior with experimental API.

**Fix**: Update to stable models per plan:
```csharp
public string ProModel { get; set; } = "gemini-1.5-pro";
public string FlashLiteModel { get; set; } = "gemini-1.5-flash";
```

### 11. **Retry Configuration Not Used** 🟡
**File**: `GeminiRetryHandler.cs:19`, `GeminiOptions.cs:12`
```csharp
retryCount: 3,  // Hardcoded
// vs
public int MaxRetries { get; set; } = 3;  // Config
```

**Problem**: MaxRetries config value never used.

**Impact**: Configuration changes ignored.

**Fix**: Inject options into handler:
```csharp
public class GeminiRetryHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<GeminiRetryHandler> _logger;

    public GeminiRetryHandler(
        IOptions<GeminiOptions> options,
        ILogger<GeminiRetryHandler> logger)
    {
        _logger = logger;
        var maxRetries = options.Value.MaxRetries;
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                // ... rest
```

### 12. **Missing Circuit Breaker** 🟡
**File**: Plan specifies circuit breaker, not implemented

**Problem**: Plan requires circuit breaker after 5 consecutive failures. Not implemented.

**Impact**: Fails success criteria, can hammer failing API.

**Recommendation**: Add to Phase 3 or implement now using Polly:
```csharp
.AddHttpMessageHandler<GeminiRetryHandler>()
.AddPolicyHandler(Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1)));
```

### 13. **No Token Usage Tracking** 🟡
**File**: `GeminiService.cs:72-73`
```csharp
_logger.LogInformation(
    "Received response from Gemini. Tokens: {Tokens}",
    result.UsageMetadata?.TotalTokenCount ?? 0);
```

**Problem**: Logs tokens but doesn't track for cost monitoring (plan requirement).

**Impact**: Can't monitor costs, no alerting on budget overruns.

**Recommendation**: Add metrics/telemetry in Phase 7 or store in database.

### 14. **System Prompt Hardcoded** 🟡
**File**: `GeminiService.cs:96-112`

**Problem**: Vietnamese system prompt hardcoded in service. Should be configurable or in separate file.

**Impact**: Hard to update, test, or localize. Violates single responsibility.

**Recommendation**: Move to configuration or separate prompt service:
```csharp
public class GeminiOptions
{
    // ...
    public string SystemPromptPath { get; set; } = "Prompts/fashion-consultant-vi.txt";
}
```

### 15. **Missing Error Response Handling** 🟡
**File**: `GeminiService.cs:59-65`
```csharp
if (!response.IsSuccessStatusCode)
{
    var error = await response.Content.ReadAsStringAsync(cancellationToken);
    _logger.LogError("Gemini API error: {StatusCode} - {Error}",
        response.StatusCode, error);
    throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
}
```

**Problem**: Doesn't parse Gemini error response structure. Loses error details.

**Impact**: Poor debugging experience, can't handle specific error types.

**Fix**: Parse error response:
```csharp
if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

    try
    {
        var errorResponse = JsonSerializer.Deserialize<GeminiErrorResponse>(errorContent);
        _logger.LogError(
            "Gemini API error: {StatusCode} - {Message} (Code: {Code})",
            response.StatusCode,
            errorResponse?.Error?.Message,
            errorResponse?.Error?.Code);

        throw new GeminiApiException(
            response.StatusCode,
            errorResponse?.Error?.Message ?? "Unknown error",
            errorResponse?.Error?.Code);
    }
    catch (JsonException)
    {
        _logger.LogError("Gemini API error: {StatusCode} - {RawError}",
            response.StatusCode, errorContent);
        throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
    }
}
```

---

## Low Priority

### 16. **Inconsistent Naming** 🔵
**File**: `GeminiModelType.cs:5-6`
```csharp
Pro,
FlashLite
```

**Problem**: "FlashLite" vs "Flash-Lite" in config. Inconsistent casing.

**Impact**: Minor confusion.

**Recommendation**: Use consistent naming (FlashLite everywhere or Flash everywhere).

### 17. **Missing XML Documentation** 🔵
**Files**: All service interfaces and public methods

**Problem**: No XML comments for public API surface.

**Impact**: Poor IntelliSense experience.

**Recommendation**: Add XML docs:
```csharp
/// <summary>
/// Sends a message to Gemini API and returns the response.
/// </summary>
/// <param name="userId">Unique identifier for the user</param>
/// <param name="message">User's message text</param>
/// <param name="history">Previous conversation messages (max 10 used)</param>
/// <param name="modelOverride">Optional model selection override</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Gemini's response text</returns>
/// <exception cref="ArgumentException">If message is invalid</exception>
/// <exception cref="HttpRequestException">If API call fails</exception>
public async Task<string> SendMessageAsync(...)
```

### 18. **Unused GeminiRequest Model** 🔵
**File**: `GeminiRequest.cs`

**Problem**: Model defined but never used (anonymous objects used instead).

**Impact**: Dead code, confusion.

**Recommendation**: Either use it or remove it:
```csharp
var request = new GeminiRequest
{
    Contents = BuildContents(message, history),
    SystemInstruction = new SystemInstruction
    {
        Parts = new[] { new Part { Text = GetSystemPrompt() } }
    },
    GenerationConfig = new GenerationConfig
    {
        Temperature = _options.Temperature,
        MaxOutputTokens = _options.MaxTokens
    }
};
```

### 19. **Part Class Missing in GeminiResponse** 🔵
**File**: `GeminiResponse.cs:27`

**Problem**: References `Part[]` but Part class not defined in same file.

**Impact**: Inconsistent model structure.

**Fix**: Add Part class:
```csharp
public class Part
{
    public string? Text { get; set; }
}
```

---

## Positive Observations

✅ **Clean Architecture**: Proper separation of concerns with handlers, strategies, models
✅ **DI Pattern**: Correct use of ASP.NET Core dependency injection
✅ **HttpClient Factory**: Proper HttpClient lifecycle management
✅ **Delegating Handlers**: Clean implementation of auth and retry logic
✅ **Strategy Pattern**: Extensible model selection design
✅ **Logging**: Good structured logging throughout
✅ **Async/Await**: Proper async patterns, no blocking calls
✅ **Configuration**: Strongly-typed options pattern
✅ **Retry Logic**: Exponential backoff with jitter implemented correctly

---

## Recommended Actions

### Immediate (Before Merge)
1. **Remove exposed credentials** from appsettings.json (Issue #2)
2. **Add Gemini API key validation** at startup (Issue #1)
3. **Fix CancellationToken warning** with [EnumeratorCancellation] (Issue #4)
4. **Add null safety** in response parsing (Issue #3)
5. **Add input validation** to SendMessageAsync (Issue #8)

### Before Production
6. **Implement rate limiting** (Issue #6)
7. **Fix timeout configuration** to use options (Issue #7)
8. **Correct model names** to stable versions (Issue #10)
9. **Use MaxRetries config** in retry handler (Issue #11)
10. **Add circuit breaker** per plan (Issue #12)
11. **Improve error handling** with structured error responses (Issue #15)

### Phase 3 Enhancements
12. **Implement real streaming** or remove placeholder (Issue #5)
13. **Add token usage tracking** for cost monitoring (Issue #13)
14. **Move system prompt** to configuration (Issue #14)
15. **Add history truncation logging** (Issue #9)

### Nice to Have
16. **Add XML documentation** (Issue #17)
17. **Remove or use GeminiRequest model** (Issue #18)
18. **Fix naming consistency** (Issue #16)

---

## Metrics

- **Build Status**: ✅ Success (1 warning)
- **Compilation Errors**: 0
- **Warnings**: 1 (CS8425 - CancellationToken)
- **Critical Issues**: 3
- **High Priority**: 5
- **Medium Priority**: 7
- **Low Priority**: 4
- **Code Coverage**: Not measured (no tests yet)
- **LOC**: 326 lines

---

## Plan TODO Status

Checking against plan file TODO list:

- ✅ Install Google Gen AI SDK and Polly packages
- ✅ Create GeminiOptions configuration model
- ✅ Implement GeminiAuthHandler for API key injection
- ✅ Implement GeminiRetryHandler with exponential backoff
- ✅ Create IGeminiService interface
- ⚠️ Implement GeminiService with streaming support (placeholder only)
- ✅ Create model selection strategy (hybrid approach)
- ✅ Register services in DI container
- ✅ Add Gemini configuration to appsettings.json
- ❌ Set API key in User Secrets (not validated)
- ❌ Implement circuit breaker pattern (not done)
- ❌ Write unit tests for service and strategy (not done)
- ❌ Integration test with real API (not done)
- ❌ Test Vietnamese language support (not done)
- ❌ Document API usage and cost tracking (not done)

**Completion**: 8/15 tasks (53%)

---

## Unresolved Questions

1. **Why use experimental model (2.0-flash-exp) instead of stable 1.5 models?**
2. **Is streaming required for Phase 2 or can it be deferred to Phase 3?**
3. **Where should token usage metrics be stored for cost tracking?**
4. **Should system prompts support multiple languages or stay Vietnamese-only?**
5. **What's the plan for handling Gemini API safety filters (HARM_CATEGORY_*)?**

---

## Security Checklist

- ❌ API keys not in source control (FAILED - Facebook creds exposed)
- ⚠️ API keys validated at startup (NOT IMPLEMENTED)
- ✅ HTTPS only for API calls
- ⚠️ Input validation (MISSING)
- ⚠️ Output sanitization (NOT IMPLEMENTED)
- ✅ Timeout configured
- ⚠️ Rate limiting (NOT IMPLEMENTED)
- ✅ Retry with backoff
- ⚠️ Circuit breaker (NOT IMPLEMENTED)
- ✅ Structured logging (no sensitive data logged)

---

## Next Steps

1. Address critical security issues (#1, #2)
2. Fix high priority reliability issues (#4, #6, #7, #8)
3. Decide on streaming implementation timeline
4. Write unit tests (delegate to tester agent)
5. Update plan file with actual completion status
6. Proceed to Phase 3: State Machine after fixes

---

**Review Status**: ⚠️ CONDITIONAL APPROVAL
**Recommendation**: Fix critical issues before merge, high priority before production deployment.
