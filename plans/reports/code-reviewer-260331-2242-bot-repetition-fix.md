# Code Review: Bot Repetition Fix Implementation

**Reviewer:** code-reviewer
**Date:** 2026-03-31 22:42
**Scope:** Bot repetition fix (4 phases)
**Commit:** ddcb0db (feat: replace brittle if-else with AI intent detection)

---

## Executive Summary

**Overall Assessment:** ⚠️ **NEEDS FIXES** - Implementation is functionally sound but has **critical edge cases** and **missing test coverage** that could cause production issues.

**Risk Level:** MEDIUM - Auto-close logic works but lacks safeguards for edge cases.

---

## Critical Issues (MUST FIX)

### 1. **Uninitialized consultationAttempts Counter** 🔴
**File:** `SalesStateHandlerBase.cs:131-163`

**Problem:**
```csharp
var consultationAttempts = ctx.GetData<int>("consultationAttempts");
if (consultationAttempts >= SalesBotOptions.MaxConsultationAttempts && ...)
```

`GetData<int>` returns `0` when key doesn't exist (default value), but this is **implicit behavior**. If the key is never set, the counter stays at 0 forever.

**Impact:** Auto-close logic never triggers if consultation tracking fails to initialize.

**Fix:**
```csharp
var consultationAttempts = ctx.GetData<int?>("consultationAttempts") ?? 0;
```

Or explicitly initialize in greeting handler:
```csharp
ctx.SetData("consultationAttempts", 0);
```

---

### 2. **Race Condition: consultationAttempts Increment After Response** 🔴
**File:** `SalesStateHandlerBase.cs:286-301`

**Problem:**
```csharp
var response = await GeminiService.SendMessageAsync(...);

// Increment happens AFTER AI response
if (isConsultationQuestion)
{
    var attempts = ctx.GetData<int>("consultationAttempts");
    ctx.SetData("consultationAttempts", attempts + 1);
}
```

**Scenario:**
1. Bot asks consultation question (attempt #1)
2. Customer says "không" immediately
3. Next handler reads `consultationAttempts = 1` (not yet incremented)
4. Auto-close check at line 132 sees `1 < 2`, doesn't trigger
5. Bot asks AGAIN (attempt #2)
6. Customer says "không" again
7. Now `consultationAttempts = 2`, auto-close triggers

**Result:** Bot still asks twice instead of once.

**Fix:** Increment BEFORE checking auto-close:
```csharp
// In BuildNaturalReplyAsync, increment first
if (isConsultationQuestion)
{
    var attempts = ctx.GetData<int>("consultationAttempts");
    ctx.SetData("consultationAttempts", attempts + 1);
}

// Then in HandleSalesConversationAsync, check happens after increment
var consultationAttempts = ctx.GetData<int>("consultationAttempts");
if (consultationAttempts >= SalesBotOptions.MaxConsultationAttempts && ...)
```

**OR** move increment to BEFORE AI call in line 283.

---

### 3. **Missing Null Check for recentHistory** 🔴
**File:** `SalesStateHandlerBase.cs:107`

**Problem:**
```csharp
var recentHistory = GetHistory(ctx).TakeLast(3).ToList();
var intentResult = await GeminiService.DetectIntentAsync(
    message, ctx.CurrentState, hasProduct, hasContact, recentHistory, ...);
```

`GetHistory` returns empty list if no history exists, but `DetectIntentAsync` accepts `List<AiConversationMessage>?` (nullable). The contract is unclear.

**Impact:** If `GetHistory` returns null (edge case), NullReferenceException.

**Fix:**
```csharp
var recentHistory = GetHistory(ctx)?.TakeLast(3).ToList() ?? new List<AiConversationMessage>();
```

Or ensure `GetHistory` never returns null (current implementation does return empty list, so this is defensive).

---

### 4. **consultationAttempts Never Reset** 🔴
**File:** `SalesStateHandlerBase.cs` (missing logic)

**Problem:** Once `consultationAttempts` reaches 2, it stays at 2 forever. If customer returns later for a new order, the counter is still 2.

**Scenario:**
1. Customer declines consultation twice → `consultationAttempts = 2`
2. Order completes → state moves to `Complete`
3. Customer returns next day → new session starts
4. Counter is still 2 → auto-close triggers immediately on first "không"

**Impact:** Returning customers get poor experience (no consultation offered).

**Fix:** Reset counter when order completes or session resets:
```csharp
// In CompleteStateHandler or when transitioning to Complete
ctx.SetData("consultationAttempts", 0);
ctx.SetData("consultationDeclined", false);
```

---

## High Priority (SHOULD FIX)

### 5. **AI Intent Detection Timeout Too Aggressive**
**File:** `GeminiService.cs:199`

```csharp
cts.CancelAfter(TimeSpan.FromMilliseconds(500));
```

**Problem:** 500ms timeout for AI intent detection is very tight. If Gemini API is slow (p95 latency), fallback triggers frequently.

**Impact:** Intent detection falls back to `Consulting` often, defeating the purpose of AI routing.

**Recommendation:** Increase to 1000ms (1 second) or make configurable:
```csharp
cts.CancelAfter(TimeSpan.FromMilliseconds(_options.IntentDetectionTimeoutMs ?? 1000));
```

---

### 6. **No Logging for Auto-Close Trigger**
**File:** `SalesStateHandlerBase.cs:136-163`

**Problem:** Auto-close logic executes silently. No log when it triggers.

**Impact:** Hard to debug if auto-close fires unexpectedly or doesn't fire when expected.

**Fix:**
```csharp
if (consultationAttempts >= SalesBotOptions.MaxConsultationAttempts && ...)
{
    Logger.LogInformation(
        "Auto-closing after {Attempts} consultation attempts - PSID: {PSID}, Intent: {Intent}",
        consultationAttempts, ctx.FacebookPSID, intentResult.Intent);
    // ... rest of logic
}
```

(Note: Logging exists at line 137-139, but should also log when NOT triggering for comparison)

---

### 7. **Inconsistent Intent Confidence Threshold Usage**
**File:** `SalesStateHandlerBase.cs:125-128`

```csharp
var useAiIntent = intentResult.Confidence >= SalesBotOptions.IntentConfidenceThreshold;
var nextState = useAiIntent
    ? DetermineNextState(intentResult.Intent, hasProduct, hasContact)
    : (hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting);
```

**Problem:** When confidence is low, fallback logic is simplistic (just checks `hasProduct`). This ignores the AI's low-confidence guess entirely.

**Recommendation:** Use weighted fallback:
```csharp
var nextState = useAiIntent
    ? DetermineNextState(intentResult.Intent, hasProduct, hasContact)
    : DetermineNextState(intentResult.Intent, hasProduct, hasContact); // Still use intent, just log low confidence
```

Or at least log when falling back:
```csharp
if (!useAiIntent)
{
    Logger.LogWarning("Low confidence intent detection ({Confidence}) for PSID {PSID}, using fallback",
        intentResult.Confidence, ctx.FacebookPSID);
}
```

---

### 8. **ConversationHistoryLimit Not Enforced**
**File:** `SalesBotOptions.cs:17`

```csharp
public int ConversationHistoryLimit { get; set; } = 15;
```

**Problem:** This config exists but is never used. History grows unbounded in `StateContext.Data["conversationHistory"]`.

**Impact:** Memory leak in long conversations. StateContext grows indefinitely.

**Fix:** Trim history in `AddToHistory`:
```csharp
protected static void AddToHistory(StateContext ctx, string role, string content)
{
    var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory")
        ?? new List<AiConversationMessage>();
    history.Add(new AiConversationMessage { Role = role, Content = content, Timestamp = DateTime.UtcNow });

    // Enforce limit
    var limit = 15; // Or inject SalesBotOptions
    if (history.Count > limit)
    {
        history = history.Skip(history.Count - limit).ToList();
    }

    ctx.SetData("conversationHistory", history);
}
```

---

## Medium Priority (NICE TO HAVE)

### 9. **Magic Number: MaxConsultationAttempts = 2**
**File:** `SalesBotOptions.cs:16`

**Problem:** Why 2? No comment explaining the business logic.

**Recommendation:** Add comment:
```csharp
// Allow 2 consultation attempts before auto-closing to avoid repetitive questions
// Based on user feedback: 4 repetitions was too many, 2 is acceptable
public int MaxConsultationAttempts { get; set; } = 2;
```

---

### 10. **Consultation Question Detection is Brittle**
**File:** `SalesStateHandlerBase.cs:286-288`

```csharp
var consultationKeywords = new[] { "cần tư vấn", "tư vấn thêm", "hỏi thêm", "thắc mắc" };
var isConsultationQuestion = consultationKeywords.Any(k => response.ToLower().Contains(k)) &&
                             response.Contains("?");
```

**Problem:** Relies on keyword matching. If AI rephrases consultation question without these exact keywords, counter doesn't increment.

**Example:** "Chị muốn em giải thích thêm về sản phẩm không?" (no matching keywords)

**Recommendation:** Use AI to detect consultation questions:
```csharp
var isConsultationQuestion = await GeminiService.DetectConsultationQuestionAsync(response);
```

Or expand keyword list based on production logs.

---

### 11. **No Test Coverage for Auto-Close Logic**
**File:** Missing tests

**Problem:** No integration tests for:
- Auto-close after 2 consultation rejections
- Counter reset on new session
- Counter increment timing
- Edge case: consultationAttempts = 1, customer says "không"

**Recommendation:** Add test file:
```csharp
// tests/MessengerWebhook.IntegrationTests/StateMachine/ConsultationAutoCloseTests.cs
[Fact]
public async Task AutoClose_After2ConsultationRejections_CreatesOrder()
{
    // Arrange: customer with product selected
    // Act: send "không" twice to consultation questions
    // Assert: order created, no 3rd consultation question
}
```

---

### 12. **Intent Detection Prompt Could Be More Specific**
**File:** `GeminiService.cs:322-329`

**Problem:** Prompt says "If bot just asked 'có cần tư vấn thêm không?' and customer says 'không'" but doesn't actually check if bot JUST asked this.

**Recommendation:** Pass last bot message to prompt:
```csharp
var lastBotMessage = recentHistory.LastOrDefault(m => m.Role == "assistant")?.Content ?? "";
var prompt = $@"...
Last bot message: ""{lastBotMessage}""
Customer response: ""{message}""
...";
```

---

## Low Priority (OPTIONAL)

### 13. **Typo in Comment**
**File:** `SalesStateHandlerBase.cs:367`

```csharp
// Case 0.5: Already asked 2+ times - don't repeat
```

Should be "Case 1.5" or "Case 0b" for consistency with other case numbers.

---

### 14. **Verbose Logging**
**File:** `SalesStateHandlerBase.cs:96-102, 116-122`

Multiple `LogInformation` calls in hot path. Consider reducing to `LogDebug` for non-critical info.

---

## Positive Observations ✅

1. **AI Intent Detection is Well-Designed** - Fallback logic, timeout handling, confidence thresholds all present
2. **Configuration is Externalized** - `MaxConsultationAttempts` and `ConversationHistoryLimit` are configurable
3. **Logging is Comprehensive** - Good visibility into intent detection results
4. **Test Coverage for Intent Detection** - `GeminiIntentDetectionTests.cs` covers all 5 intent types
5. **Mock Implementation is Correct** - `TestGeminiService` in `CustomWebApplicationFactory.cs` properly implements intent detection

---

## Missing Test Scenarios

### Integration Tests Needed:
1. **Auto-close after 2 rejections** - Customer says "không" twice → order created
2. **Auto-close with missing contact info** - Customer says "không" twice but no phone → asks for phone
3. **Counter reset on new session** - Complete order → return later → counter is 0
4. **Counter increment timing** - Verify increment happens before auto-close check
5. **Low confidence fallback** - Intent confidence < 0.7 → fallback logic used
6. **History trimming** - Send 20 messages → history limited to 15
7. **Consultation question detection** - AI response with consultation keywords → counter increments

### Unit Tests Needed:
1. **GetData<int> default value** - Key doesn't exist → returns 0
2. **consultationAttempts edge cases** - Value is null, negative, very large
3. **BuildCtaContext with consultationDeclined** - Verify CTA instruction changes

---

## Recommended Actions (Prioritized)

### Immediate (Before Merge):
1. ✅ Fix uninitialized `consultationAttempts` counter (use `int?` or explicit init)
2. ✅ Fix race condition: increment counter BEFORE auto-close check
3. ✅ Add counter reset logic in `CompleteStateHandler`
4. ✅ Add integration test for auto-close after 2 rejections

### Short-term (Next Sprint):
5. Enforce `ConversationHistoryLimit` in `AddToHistory`
6. Increase intent detection timeout to 1000ms or make configurable
7. Add logging when auto-close doesn't trigger (for comparison)
8. Add test coverage for counter reset and edge cases

### Long-term (Tech Debt):
9. Replace keyword-based consultation detection with AI detection
10. Add monitoring/metrics for auto-close trigger rate
11. Consider A/B testing `MaxConsultationAttempts = 1` vs `2`

---

## Metrics

- **Files Changed:** 6 core files + 2 test files
- **LOC Added:** ~150 lines (intent detection + auto-close logic)
- **Test Coverage:**
  - Intent detection: ✅ Good (5 intent types + error cases)
  - Auto-close logic: ❌ Missing (0 tests)
  - Counter management: ❌ Missing (0 tests)
- **Linting Issues:** 0 (code compiles)
- **Type Safety:** ✅ Good (no `dynamic` or `object` abuse)

---

## Unresolved Questions

1. **Business Logic:** Why `MaxConsultationAttempts = 2` specifically? Was this A/B tested?
2. **Counter Scope:** Should `consultationAttempts` reset per product or per session?
3. **Intent Confidence:** Is 0.7 threshold validated? What's the false positive rate?
4. **History Limit:** Why 15 messages? Based on token limits or UX research?
5. **Consultation Keywords:** Are these keywords exhaustive? Should we add more based on logs?

---

## Security & Privacy

- ✅ No PII leakage in logs
- ✅ No secrets in config (API keys externalized)
- ✅ Input validation present (message length check in `GeminiService.cs:56`)
- ✅ No SQL injection risk (using EF Core)
- ✅ No auth bypass (tenant isolation maintained)

---

## Performance

- ⚠️ **N+1 Risk:** `GetHistory(ctx)` called multiple times per request (lines 107, 213, 258)
  - **Fix:** Cache result: `var history = GetHistory(ctx); // reuse`
- ⚠️ **Unbounded History Growth:** No trimming → memory leak in long conversations
- ✅ AI timeout present (500ms) → prevents hanging requests
- ✅ No blocking I/O in hot path

---

## Backwards Compatibility

- ✅ No breaking changes to `IGeminiService` interface (added optional parameter)
- ✅ No DB schema changes
- ✅ Config changes are additive (new keys, no removals)
- ✅ Existing tests pass (per commit message)

---

## Conclusion

Implementation is **functionally correct** but has **critical edge cases** around counter management that could cause production issues. The auto-close logic works as designed, but lacks safeguards for:
- Uninitialized counters
- Race conditions in increment timing
- Counter reset on new sessions

**Recommendation:** Fix critical issues #1-4 before merging. Add integration tests for auto-close logic. Monitor production logs for auto-close trigger rate after deployment.

**Estimated Fix Time:** 2-3 hours (fixes + tests)

---

**Status:** ⚠️ NEEDS FIXES
**Next Reviewer:** tester (after fixes applied)
