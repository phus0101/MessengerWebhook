---
title: "AI Intent Detection System for Order Creation Flow"
description: "Replace brittle if-else logic with AI-based intent detection to prevent premature order creation"
status: pending
priority: P1
effort: 6h
branch: master
tags: [ai, gemini, intent-detection, sales-flow, bug-fix]
created: 2026-03-31
blockedBy: []
blocks: [260420-1343-ai-hot-path-timeout-orchestration]
---

# AI Intent Detection System Implementation Plan

## Problem Statement

**Current Issue**: Bot auto-creates orders when customer provides contact info, even when they explicitly say "cần tư vấn trước khi đặt hàng" (need consultation before ordering).

**Root Cause**: Lines 103-112 in `SalesStateHandlerBase.cs` only check `HasSelectedProduct && HasRequiredContact`, ignoring customer intent signals.

**Impact**: Poor UX, customer complaints, premature order creation disrupts consultation flow.

## Solution Overview

Implement AI-based intent detection using Gemini FlashLite to classify customer messages into 5 intent categories, replacing brittle if-else logic with context-aware routing.

**Architecture Pattern**: Follow existing `DetectConfirmationAsync` pattern (lines 152-256 in GeminiService.cs) for consistency.

## Intent Categories

```csharp
public enum CustomerIntent
{
    Browsing,        // "tính xem", "xem thử" - exploring products
    Consulting,      // "cần tư vấn", "hỏi" - needs advice before buying
    ReadyToBuy,      // "đặt hàng", "chốt đơn" - ready to place order
    Confirming,      // "đúng rồi", "ok" - confirming previous info
    Questioning      // "ship bao lâu?", "giá bao nhiêu?" - asking questions
}
```

## Data Flow

```
Customer Message
    ↓
[Extract Context]
    ├─ Current State
    ├─ Has Product?
    └─ Has Contact?
    ↓
[DetectIntentAsync] ← Gemini FlashLite (500ms timeout)
    ↓
IntentDetectionResult
    ├─ Intent: CustomerIntent
    ├─ Confidence: 0.0-1.0
    └─ Reason: string
    ↓
[Route by Intent]
    ├─ Browsing → Continue consulting
    ├─ Consulting → Enter consulting mode
    ├─ ReadyToBuy → Create order (if has product + contact)
    ├─ Confirming → Context-dependent action
    └─ Questioning → Answer question, stay in current state
```

## Success Criteria

- [ ] Bot detects "cần tư vấn" and enters consulting mode instead of creating order
- [ ] Bot only creates order when intent = ReadyToBuy AND has product + contact
- [ ] All existing tests pass
- [ ] New tests cover all 5 intent types with Vietnamese examples
- [ ] P95 latency < 500ms (same as confirmation detection)
- [ ] Fallback to conservative behavior on timeout/error
- [ ] Feature flag for rollback: `EnableAiIntentDetection`

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| AI misclassifies "đúng rồi" as Confirming instead of ReadyToBuy | Medium | High | Confidence threshold 0.7, fallback to conservative routing |
| Timeout degrades UX | Low | Medium | 500ms timeout, fallback returns Consulting (safe default) |
| Increased Gemini API costs | Low | Low | FlashLite model is cost-effective, same as confirmation detection |
| Breaking existing flows | Medium | High | Feature flag, comprehensive tests, gradual rollout |

## Implementation Phases

### Phase 1: Create Intent Detection Models (30min)
**Files to Create**:
- `src/MessengerWebhook/Services/AI/Models/CustomerIntent.cs`
- `src/MessengerWebhook/Services/AI/Models/IntentDetectionResult.cs`

**Tasks**:
- Define `CustomerIntent` enum with 5 values
- Create `IntentDetectionResult` class matching `ConfirmationDetectionResult` pattern
- Add XML documentation with Vietnamese examples
- Include `DetectionMethod` field for observability

**Success Criteria**:
- Models compile without errors
- XML docs include Vietnamese examples for each intent

---

### Phase 2: Add Interface Method (15min)
**Files to Modify**:
- `src/MessengerWebhook/Services/AI/IGeminiService.cs`

**Tasks**:
- Add `DetectIntentAsync` method signature
- Follow same pattern as `DetectConfirmationAsync` (lines 23-37)
- Include XML documentation with latency expectations

**Method Signature**:
```csharp
/// <summary>
/// Detects customer intent from message using AI reasoning.
/// Uses Gemini FlashLite for fast, context-aware classification.
/// Expected latency: <500ms (p95).
/// </summary>
Task<IntentDetectionResult> DetectIntentAsync(
    string message,
    ConversationState currentState,
    bool hasProduct,
    bool hasContact,
    CancellationToken cancellationToken = default);
```

**Success Criteria**:
- Interface compiles
- Method signature matches pattern

---

### Phase 3: Implement DetectIntentAsync in GeminiService (2h)
**Files to Modify**:
- `src/MessengerWebhook/Services/AI/GeminiService.cs`
- `src/MessengerWebhook/Configuration/GeminiOptions.cs`

**Tasks**:
1. Add feature flag to `GeminiOptions`:
   ```csharp
   public bool EnableAiIntentDetection { get; set; } = true;
   public double IntentConfidenceThreshold { get; set; } = 0.7;
   ```

2. Implement `DetectIntentAsync` following `DetectConfirmationAsync` pattern (lines 152-256):
   - Check feature flag first
   - Build prompt with Vietnamese examples
   - 500ms timeout using `CancellationTokenSource`
   - Call Gemini FlashLite model
   - Parse JSON response
   - Handle timeout/errors with fallback

3. Create `FallbackIntentResult` helper (similar to line 258):
   ```csharp
   private static IntentDetectionResult FallbackIntentResult(string reason)
   {
       return new IntentDetectionResult
       {
           Intent = CustomerIntent.Consulting, // Conservative default
           Confidence = 0.0,
           Reason = reason,
           DetectionMethod = "fallback"
       };
   }
   ```

**Prompt Engineering**:
```csharp
var prompt = $@"You are a Vietnamese customer service intent classifier for a cosmetics e-commerce chatbot.

Customer message: ""{message}""
Context:
- Current state: {currentState}
- Has selected product: {hasProduct}
- Has provided contact: {hasContact}

Task: Classify the customer's intent into ONE of these categories:

1. Browsing - Customer is exploring, not committed
   Examples: ""tính xem"", ""xem thử"", ""có màu nào""

2. Consulting - Customer needs advice before deciding
   Examples: ""cần tư vấn"", ""chị tư vấn giúp em"", ""hỏi trước""

3. ReadyToBuy - Customer is ready to place order
   Examples: ""đặt hàng"", ""chốt đơn"", ""lên đơn luôn""

4. Confirming - Customer is confirming previous information
   Examples: ""đúng rồi"", ""ok em"", ""vẫn dùng""

5. Questioning - Customer is asking a question
   Examples: ""ship bao lâu?"", ""giá bao nhiêu?"", ""có freeship không?""

IMPORTANT:
- If message contains ""cần tư vấn"" or ""tư vấn trước"" → Consulting
- If message is just ""đúng rồi"" after providing contact → Confirming
- If message is ""đúng rồi, nhưng cần tư vấn"" → Consulting (takes priority)

Respond ONLY with valid JSON:
{{
  ""intent"": ""Browsing|Consulting|ReadyToBuy|Confirming|Questioning"",
  ""confidence"": 0.0-1.0,
  ""reason"": ""brief explanation in English""
}}";
```

**Error Handling**:
- Timeout → Fallback to Consulting
- API error → Fallback to Consulting
- Invalid JSON → Fallback to Consulting
- Low confidence (<0.7) → Log warning, use detected intent but flag for review

**Success Criteria**:
- Method compiles and follows existing pattern
- Feature flag controls behavior
- Timeout works correctly
- Fallback returns safe default
- Logging includes confidence and detected intent

---

### Phase 4: Refactor HandleSalesConversationAsync (2h)
**Files to Modify**:
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

**Current Logic (lines 103-112)**:
```csharp
if (HasSelectedProduct(ctx) && SalesMessageParser.HasRequiredContact(ctx))
{
    var draft = await DraftOrderService.CreateFromContextAsync(ctx);
    // ... create order
}
```

**New Logic**:
```csharp
// Detect customer intent
var intentResult = await GeminiService.DetectIntentAsync(
    message,
    ctx.CurrentState,
    HasSelectedProduct(ctx),
    SalesMessageParser.HasRequiredContact(ctx),
    cancellationToken: default
);

Logger.LogInformation(
    "Intent detected for PSID {PSID}: {Intent} (confidence: {Confidence})",
    ctx.FacebookPSID, intentResult.Intent, intentResult.Confidence
);

// Route based on intent
switch (intentResult.Intent)
{
    case CustomerIntent.ReadyToBuy:
        // Only create order if has product + contact
        if (HasSelectedProduct(ctx) && SalesMessageParser.HasRequiredContact(ctx))
        {
            var draft = await DraftOrderService.CreateFromContextAsync(ctx);
            ctx.SetData("draftOrderId", draft.Id);
            ctx.SetData("draftOrderCode", draft.DraftCode);
            ctx.CurrentState = ConversationState.Complete;

            var confirmation = BuildDraftConfirmation(draft);
            AddToHistory(ctx, "assistant", confirmation);
            return confirmation;
        }
        else
        {
            // Missing info, ask for it
            ctx.CurrentState = ConversationState.CollectingInfo;
            var reply = await BuildNaturalReplyAsync(ctx, message);
            AddToHistory(ctx, "assistant", reply);
            return reply;
        }

    case CustomerIntent.Consulting:
        // Enter consulting mode, don't create order
        ctx.CurrentState = ConversationState.Consulting;
        var consultingReply = await BuildNaturalReplyAsync(ctx, message);
        AddToHistory(ctx, "assistant", consultingReply);
        return consultingReply;

    case CustomerIntent.Browsing:
        // Continue conversation, don't create order
        ctx.CurrentState = ConversationState.Consulting;
        var browsingReply = await BuildNaturalReplyAsync(ctx, message);
        AddToHistory(ctx, "assistant", browsingReply);
        return browsingReply;

    case CustomerIntent.Confirming:
        // Context-dependent: if has product + contact, create order
        if (HasSelectedProduct(ctx) && SalesMessageParser.HasRequiredContact(ctx))
        {
            var draft = await DraftOrderService.CreateFromContextAsync(ctx);
            ctx.SetData("draftOrderId", draft.Id);
            ctx.SetData("draftOrderCode", draft.DraftCode);
            ctx.CurrentState = ConversationState.Complete;

            var confirmation = BuildDraftConfirmation(draft);
            AddToHistory(ctx, "assistant", confirmation);
            return confirmation;
        }
        else
        {
            // Confirming something else, continue conversation
            ctx.CurrentState = ConversationState.CollectingInfo;
            var reply = await BuildNaturalReplyAsync(ctx, message);
            AddToHistory(ctx, "assistant", reply);
            return reply;
        }

    case CustomerIntent.Questioning:
        // Answer question, stay in current state
        var questionReply = await BuildNaturalReplyAsync(ctx, message);
        AddToHistory(ctx, "assistant", questionReply);
        return questionReply;

    default:
        // Fallback to conservative behavior
        Logger.LogWarning("Unknown intent: {Intent}, falling back to consulting mode", intentResult.Intent);
        ctx.CurrentState = ConversationState.Consulting;
        var fallbackReply = await BuildNaturalReplyAsync(ctx, message);
        AddToHistory(ctx, "assistant", fallbackReply);
        return fallbackReply;
}
```

**Backwards Compatibility**:
- Feature flag `EnableAiIntentDetection` controls new behavior
- If disabled, fall back to old if-else logic
- Existing tests should pass with flag disabled

**Success Criteria**:
- Order only created when intent = ReadyToBuy or Confirming (with context)
- Consulting intent prevents order creation
- All state transitions logged
- Feature flag works correctly

---

### Phase 5: Add Configuration (15min)
**Files to Modify**:
- `src/MessengerWebhook/appsettings.json`
- `src/MessengerWebhook/appsettings.Development.json`

**Tasks**:
- Add `EnableAiIntentDetection: true`
- Add `IntentConfidenceThreshold: 0.7`

**Example**:
```json
"Gemini": {
  "EnableAiConfirmationDetection": true,
  "ConfirmationConfidenceThreshold": 0.7,
  "EnableAiIntentDetection": true,
  "IntentConfidenceThreshold": 0.7
}
```

**Success Criteria**:
- Configuration loads correctly
- Feature flag can be toggled

---

### Phase 6: Write Unit Tests (1.5h)
**Files to Create**:
- `tests/MessengerWebhook.Tests/Services/AI/GeminiServiceIntentDetectionTests.cs`
- `tests/MessengerWebhook.Tests/StateMachine/Handlers/SalesStateHandlerIntentRoutingTests.cs`

**Test Matrix**:

#### GeminiServiceIntentDetectionTests
1. **Feature Flag Disabled** → Returns fallback result
2. **Browsing Intent** → "tính xem" → Browsing
3. **Consulting Intent** → "cần tư vấn" → Consulting
4. **ReadyToBuy Intent** → "đặt hàng" → ReadyToBuy
5. **Confirming Intent** → "đúng rồi" → Confirming
6. **Questioning Intent** → "ship bao lâu?" → Questioning
7. **Mixed Intent** → "đúng rồi, nhưng cần tư vấn" → Consulting (priority)
8. **Timeout** → Returns fallback (Consulting)
9. **API Error** → Returns fallback (Consulting)
10. **Invalid JSON** → Returns fallback (Consulting)
11. **Low Confidence** → Uses detected intent but logs warning

#### SalesStateHandlerIntentRoutingTests
1. **ReadyToBuy + HasProduct + HasContact** → Creates order
2. **ReadyToBuy + Missing Product** → Asks for product
3. **ReadyToBuy + Missing Contact** → Asks for contact
4. **Consulting + HasProduct + HasContact** → Does NOT create order, enters consulting mode
5. **Browsing** → Continues conversation, no order
6. **Confirming + HasProduct + HasContact** → Creates order
7. **Confirming + Missing Info** → Continues conversation
8. **Questioning** → Answers question, stays in current state
9. **Feature Flag Disabled** → Falls back to old logic

**Test Pattern** (follow existing `DetectConfirmationAsync` tests):
```csharp
[Fact]
public async Task DetectIntentAsync_ConsultingMessage_ReturnsConsultingIntent()
{
    // Arrange
    var message = "chị cần tư vấn về sản phẩm trước khi đặt hàng";
    var currentState = ConversationState.CollectingInfo;
    var hasProduct = true;
    var hasContact = true;

    // Mock Gemini API response
    var mockResponse = new GeminiResponse
    {
        Candidates = new[]
        {
            new GeminiCandidate
            {
                Content = new GeminiContent
                {
                    Parts = new[]
                    {
                        new GeminiPart
                        {
                            Text = @"{
                                ""intent"": ""Consulting"",
                                ""confidence"": 0.95,
                                ""reason"": ""Customer explicitly asks for consultation before ordering""
                            }"
                        }
                    }
                }
            }
        }
    };

    _mockHttpMessageHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(mockResponse)
        });

    // Act
    var result = await _geminiService.DetectIntentAsync(
        message, currentState, hasProduct, hasContact);

    // Assert
    Assert.Equal(CustomerIntent.Consulting, result.Intent);
    Assert.True(result.Confidence >= 0.7);
    Assert.Equal("ai-reasoning", result.DetectionMethod);
}
```

**Success Criteria**:
- All tests pass
- Code coverage > 80% for new code
- Tests cover all 5 intents with Vietnamese examples
- Tests cover error scenarios (timeout, API error, invalid JSON)

---

### Phase 7: Integration Testing (1h)
**Manual Test Scenarios**:

1. **Scenario: Customer needs consultation**
   - Message: "đúng rồi, chị cần tư vấn về sản phẩm trước khi đặt hàng"
   - Expected: Bot enters consulting mode, does NOT create order
   - Verify: No draft order created, state = Consulting

2. **Scenario: Customer ready to buy**
   - Message: "ok em, chốt đơn luôn"
   - Context: Has product + contact
   - Expected: Bot creates order
   - Verify: Draft order created, state = Complete

3. **Scenario: Customer browsing**
   - Message: "tính xem thêm màu khác"
   - Expected: Bot continues conversation, no order
   - Verify: No draft order, state = Consulting

4. **Scenario: Customer asking question**
   - Message: "ship bao lâu?"
   - Expected: Bot answers question, stays in current state
   - Verify: No state change, no order

5. **Scenario: Timeout fallback**
   - Simulate: Gemini API timeout
   - Expected: Bot falls back to consulting mode (safe default)
   - Verify: No crash, state = Consulting

**Test Environment**:
- Use Development environment with real Gemini API
- Monitor logs for intent detection results
- Check P95 latency < 500ms

**Success Criteria**:
- All scenarios pass
- No crashes or errors
- Latency within acceptable range
- Logs show correct intent detection

---

## Rollback Plan

If issues arise in production:

1. **Immediate**: Set `EnableAiIntentDetection: false` in appsettings
2. **Restart**: Application picks up config change
3. **Verify**: Bot falls back to old if-else logic
4. **Monitor**: Check logs for errors, customer complaints stop
5. **Investigate**: Review logs for misclassifications
6. **Fix**: Adjust prompt or confidence threshold
7. **Re-enable**: Set flag back to true after fix

**No code deployment needed for rollback** - feature flag provides instant rollback.

## Monitoring & Observability

**Metrics to Track**:
- Intent detection latency (P50, P95, P99)
- Intent distribution (% of each intent type)
- Confidence scores (average, min, max)
- Fallback rate (% of timeouts/errors)
- Order creation rate before/after (should decrease for Consulting intent)

**Logs to Add**:
```csharp
Logger.LogInformation(
    "Intent detected for PSID {PSID}: {Intent} (confidence: {Confidence}, method: {Method})",
    ctx.FacebookPSID, intentResult.Intent, intentResult.Confidence, intentResult.DetectionMethod
);
```

**Alerts**:
- Fallback rate > 10% → Investigate Gemini API issues
- Average confidence < 0.7 → Review prompt engineering
- Order creation rate drops > 50% → Possible over-classification as Consulting

## Dependencies

**Blocked By**: None (all dependencies already in place)

**Blocks**: None (standalone feature)

**External Dependencies**:
- Gemini API (FlashLite model) - already integrated
- Existing `GeminiService` infrastructure - already in place
- Existing `SalesStateHandlerBase` - already in place

## File Ownership

| Phase | Files Modified | Owner |
|-------|---------------|-------|
| Phase 1 | `Services/AI/Models/CustomerIntent.cs`, `Services/AI/Models/IntentDetectionResult.cs` | Developer |
| Phase 2 | `Services/AI/IGeminiService.cs` | Developer |
| Phase 3 | `Services/AI/GeminiService.cs`, `Configuration/GeminiOptions.cs` | Developer |
| Phase 4 | `StateMachine/Handlers/SalesStateHandlerBase.cs` | Developer |
| Phase 5 | `appsettings.json`, `appsettings.Development.json` | Developer |
| Phase 6 | `Tests/Services/AI/GeminiServiceIntentDetectionTests.cs`, `Tests/StateMachine/Handlers/SalesStateHandlerIntentRoutingTests.cs` | Developer |
| Phase 7 | Manual testing | QA/Developer |

**No file conflicts** - all phases modify distinct files or different sections of same file.

## Migration Path

**Existing Users**: No migration needed - feature flag defaults to `true`, but fallback logic ensures no breaking changes.

**Existing Data**: No data migration needed - intent detection is stateless.

**Existing Integrations**: No integration changes - API contracts unchanged.

## Unresolved Questions

1. **Prompt Tuning**: Should we add more Vietnamese examples to the prompt based on production data?
   - **Resolution**: Start with current examples, monitor logs for misclassifications, iterate on prompt.

2. **Confidence Threshold**: Is 0.7 the right threshold, or should we start more conservative (0.8)?
   - **Resolution**: Start with 0.7 (same as confirmation detection), adjust based on production metrics.

3. **Intent Priority**: If message contains multiple intent signals, which takes priority?
   - **Resolution**: Consulting > ReadyToBuy > Questioning > Confirming > Browsing (safest to most aggressive).

4. **Caching**: Should we cache intent detection results like confirmation detection?
   - **Resolution**: No - intent is context-dependent (state, product, contact), caching could cause stale results.

5. **Fallback Intent**: Should fallback be Consulting (conservative) or Questioning (neutral)?
   - **Resolution**: Consulting - safer to over-consult than to prematurely create orders.
