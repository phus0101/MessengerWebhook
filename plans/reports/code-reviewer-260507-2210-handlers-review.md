# Code Review: StateMachine Handler Integration (feat/subintent-classification-system)

**Branch:** feat/subintent-classification-system
**Range:** master..HEAD (8090cc7e..6b47dcd8)
**Files reviewed:** 8 handlers + appsettings.json + 4 test files
**LOC delta:** +2490 / -111 (huge)

## Verdict

The branch ships SubIntent classification but bundles unrelated, much larger refactors (PolicyGuard restructuring, ProductGrounding integration, RAG context overhaul, related-suggestion selection, fact-validation overhaul). The headline feature (SubIntent) is **untested** at handler level, while infrastructure changes around it carry a real correctness bug. The base class is bloated and overdue for decomposition.

---

## CRITICAL

### 1. Duplicate user message in CompleteStateHandler policy guard request
**File:** `src/MessengerWebhook/StateMachine/Handlers/CompleteStateHandler.cs:139-149`

`AddToHistory(ctx, "user", message)` is called at line 139, then at lines 145-149 `history.TakeLast(MaxRecentTurns).Select(...).Append(new PolicyConversationTurn("user", message))` is built. Because `history` already includes the just-added user turn, the resulting `recentTurns` array contains the user's message **twice in a row**.

Compare with the base-class equivalent `BuildPolicyGuardRequest` (`SalesStateHandlerBase.cs:731-761`) which explicitly strips the duplicate:
```csharp
if (previousTurns.Count > 0 &&
    string.Equals(previousTurns[^1].Role, "user", ...) &&
    string.Equals(previousTurns[^1].Content, message, ...))
{
    previousTurns = previousTurns.Take(previousTurns.Count - 1).ToArray();
}
```
CompleteStateHandler reimplemented the same logic without that guard.

**Why it matters:** PolicyGuard config has `RepeatMentionBoost: 0.1` (`appsettings.json:113`). The duplicate causes false repeat-mention boosts on a single message, which can push borderline messages over `SoftEscalateThreshold` (0.6) or `HardEscalateThreshold` (0.8). This can silently route innocuous post-order follow-ups to human handoff.

**Fix:** Either reuse the base method (preferred — eliminates duplication), or add the same dedupe guard inline.

### 2. SubIntent feature has zero handler-level test coverage
**Files:** `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs` (all 43 tests)

Every test injects `Mock.Of<ISubIntentClassifier>()` (default mock returns null from `ClassifyAsync`). No test asserts:
- That `ClassifyAsync` is even called when `intent == Consulting`
- That `ctx.SetData("subIntent", ...)` is populated
- That `subIntentGuidance` is forwarded to `GeminiService.SendMessageAsync`
- That `includeDetailedInfo == true` when `subIntent.Category == ProductQuestion` (line 922 of base)
- Behavior when `ClassifyAsync` returns non-null

**Why it matters:** This is the headline feature of the branch. A regression where SubIntent silently stops firing (e.g., guard condition flipped, context key renamed, JSON serialization breaks the round-trip) would not be caught by CI.

**Fix:** Add at least three tests: (a) Consulting intent → `ClassifyAsync` called, guidance forwarded; (b) non-Consulting intent → `ClassifyAsync` NOT called; (c) classifier returns null → flow continues without guidance.

### 3. TestSalesStateHandler hides a real DI mistake
**File:** `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs:3164, 3210`

Both `TestSalesStateHandler` constructors accept an `ISubIntentClassifier subIntentClassifier` parameter, then **discard it** and pass `Mock.Of<ISubIntentClassifier>()` to the base. Because the parameter is silently ignored, every call site that thinks it is wiring a stubbed classifier is actually wiring `null`-returning default mock.

**Why it matters:** Worse than missing test coverage — this hides any future test that tries to verify SubIntent. The test author would assume their setup is in effect, debug the failing assertion, and waste time looking at production code.

**Fix:** Pass the constructor parameter through:
```csharp
: base(..., subIntentClassifier, ...)  // not Mock.Of<ISubIntentClassifier>()
```

---

## IMPORTANT

### 4. SalesStateHandlerBase is now 2,793 lines — beyond unmanageable
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` (entire file)

Per project rule (`CLAUDE.md` → "Consider Modularization"): files >200 LOC should be split. This file is 14× over. `HandleSalesConversationAsync` alone (lines 174-703) is a 530-line linear method with 20+ early returns, ~40 boolean flags, and at least 9 different "build * reply" sub-paths. `BuildNaturalReplyAsync` is another 300-line monolith.

The branch's net +636 lines mostly grew the same monolith rather than carving out:
- `IntentRoutingService` (the 200-line decision tree at lines 230-700)
- `ProductGroundingFallbackService` (the new fallback paths)
- `ConversationCtaPolicy` (the 120-line `BuildCtaContext` switch at lines 1469-1583)
- `RagContextResolver` (RAG retrieval + grounding sanitization)

**Why it matters:** New devs have to read all 2.8k lines to make any change safely. The combinatorial state space (intents × question types × pending flags × A/B variant) is impossible to reason about. Future bugs are inevitable.

**Fix:** Not blocking, but flag a follow-up task to extract at minimum the routing decision and CTA logic into named services with their own tests.

### 5. Cancellation tokens never propagated
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:327, 889, 961, 971, 994, 1031, 1064, 1146, 1217, 1350, 1953`

Every async call uses `CancellationToken.None`. `HandleAsync` itself takes no `CancellationToken`, and `IStateHandler.HandleAsync` interface presumably doesn't expose one. Result: a webhook caller that cancels (e.g., HTTP client disconnects, server shutting down) cannot propagate the cancellation to:
- Gemini AI calls (timeout-protected internally, but a request that has already raced 60s burns a thread)
- RAG retrieval
- ResponseValidationService.ValidateAsync (called 4× per request)
- Customer service DB lookups

**Why it matters:** Under load, cancelled webhooks still consume the full 5-30s of downstream latency, choking the thread pool. This is a known scalability cliff.

**Fix:** Plumb `CancellationToken` through `IStateHandler.HandleAsync` → `HandleInternalAsync` → `HandleSalesConversationAsync`. Touches every handler but mechanical. Not blocking the SubIntent feature itself.

### 6. SubIntent is classified but mostly thrown away
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:366-381`

SubIntent classification fires only when `intentResult.Intent == Consulting` (line 368), but it lives in the same method that ALSO has hardcoded keyword-based question routers (lines 415-441) for product/price/inventory/shipping/policy/contact-memory/order-estimate questions. Those routers short-circuit with canned replies (lines 517-569, 484-515) and **ignore SubIntent entirely**.

So the practical flow for a "giá bao nhiêu?" (price question) message is:
1. Intent classified as Consulting (correct)
2. SubIntent classified as `PriceQuestion` (correct, ~500ms latency)
3. Stored in context, logged, **ignored** because `isPriceQuestion` keyword router fires first at line 555

SubIntent guidance only takes effect when the message reaches the AI fallback path through `BuildNaturalReplyAsync`. That happens for ambiguous/conversational queries — exactly the cases where the keyword detector inside `HybridSubIntentClassifier` is least confident, meaning the slower AI fallback path is most likely to be taken.

**Why it matters:** Adds ~500ms latency to a majority of consulting turns for negligible improvement on most of them. If the goal of SubIntent is to drive specialized replies, the canned routers and SubIntent should be unified — SubIntent should drive routing, or the keyword routers should be deleted.

**Fix:** Decide which is authoritative. Either:
- (a) Use `subIntent.Category` to pick the canned reply path (`PriceQuestion → BuildPriceConsultationReplyAsync`), and skip SubIntent classification when keyword-router pre-filters short-circuit; OR
- (b) Delete the canned routers and let `BuildNaturalReplyAsync` always handle, with SubIntent guidance.

### 7. SubIntentResult round-trip risk if StateContext is persisted as JSON
**File:** `src/MessengerWebhook/StateMachine/Models/StateContext.cs:16-42` + `src/MessengerWebhook/Services/SubIntent/SubIntentResult.cs:6-25`

`SubIntentResult` declares `Category` and `Confidence` as `required` init properties. `StateContext.GetData<T>` does `JsonSerializer.Deserialize<T>(...)` and silently `return default` on any deserialization exception (line 40). If StateContext is ever serialized to a distributed cache or DB and re-hydrated, deserialization needs proper JSON for required members. Failure mode: SubIntent silently disappears between turns without log noise.

**Why it matters:** This currently doesn't bite because StateContext seems in-memory in this branch, but as soon as scaling pushes for Redis-backed sessions, a subtle data loss appears. Worth a `try/catch` log instead of silent default.

**Fix:** At minimum, log on JSON deserialization failure in `StateContext.GetData<T>`. Better: tag SubIntent storage with a contract test that round-trips through JSON.

### 8. Classification done without conversation context or cancellation
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:370`

```csharp
subIntent = await SubIntentClassifier.ClassifyAsync(message);
```
The interface accepts a `ConversationContext` parameter for disambiguation (`ISubIntentClassifier.cs:16`) and a `CancellationToken`. Neither is passed. The base handler has access to:
- `ctx.CurrentState` (for `CurrentState`)
- `HasSelectedProduct(ctx)` (for `HasProduct`)
- `GetHistory(ctx).TakeLast(5)` (for `RecentHistory`)

**Why it matters:** The classifier was designed to disambiguate using context. Calling without it forfeits the design. This makes SubIntent worse at exactly the cases where keyword detection fails — the AI fallback is the costly path.

**Fix:** Build and pass `ConversationContext` from already-available data. Trivially low effort.

---

## MINOR

### 9. `RequiresProductGrounding(string message)` overload is dead code
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:2173-2179`

Single-arg overload added but never called. The 8-arg overload (line 2152) is the only one used. YAGNI.

**Fix:** Delete.

### 10. `_productGroundingService` fallback bypasses DI
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:150`

```csharp
_productGroundingService = productGroundingService
    ?? new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector());
```
Constructing concrete services inside a constructor defeats DI substitutability and tests can be silently bypassed if a derived handler skips the parameter. Make `IProductGroundingService` non-nullable in the constructor and let DI fail loudly if missing.

**Fix:** Drop the fallback; require DI to provide it.

### 11. `BuildNaturalReplyAsync` reads SubIntent twice via different sources
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:884, 921, 1082`

The method takes `subIntent` as a parameter (line 884), but also reads `ctx.GetData<SubIntentResult?>("subIntent")` at line 921 to compute `includeDetailedInfo`. Today they are the same value (set at line 379, passed at line 700). The dual access is a refactor hazard: anyone moving SubIntent classification could break one path silently.

**Fix:** Use the parameter consistently — pass `subIntent?.Category == SubIntentCategory.ProductQuestion` for `includeDetailedInfo`.

### 12. `EscalationKeywords` removed from `SalesBotOptions` without migration note
**File:** `src/MessengerWebhook/appsettings.json:107` (line removed)

The `SalesBot:EscalationKeywords` CSV-string property was deleted. Production environments setting this via env var or override config will silently lose the data. The new `PolicyGuard:EscalationKeywords` is an empty array; `KeywordPolicySignalDetector` retains a hardcoded `BuiltInKeywords` list as fallback, so behavior largely matches if defaults sufficed.

**Why it matters:** Operators with custom escalation keywords get silent regression at deploy.

**Fix:** Add a `project-changelog.md` entry calling out the move from `SalesBot:EscalationKeywords` (string) to `PolicyGuard:EscalationKeywords` (array). Optionally log a startup warning if `SalesBot:EscalationKeywords` is set in raw config.

### 13. A/B control variant skips SubIntent guidance entirely
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:898-907, 1260-1361`

`GenerateDirectAIResponseAsync` (control branch) does not pass `subIntentGuidance` to `GeminiService.SendMessageAsync`. The only mention of SubIntent in `BuildNaturalReplyAsync` (treatment branch) is the `subIntentGuidance` switch.

**Why it matters:** A/B comparison conflates two changes (full naturalness pipeline + SubIntent guidance). Cannot attribute lift to either independently.

**Fix:** Either feed SubIntent guidance into both variants, or document this confound in the A/B test design notes.

### 14. PolicyGuard fallback ctor in `SalesHandlerFallbacks` uses defaults that diverge from configured options
**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesHandlerFallbacks.cs:18-19`

`SalesHandlerFallbacks.PolicyGuardService` is initialized with `Options.Create(new SalesBotOptions())` and the simplified `PolicyGuardService(IOptions<SalesBotOptions>)` constructor, which builds detectors with brand-new `PolicyGuardOptions()` defaults. So `IdleStateHandler`'s simplified ctor uses defaults that don't match `appsettings.json:106-122`.

**Why it matters:** If `IdleStateHandler` is ever resolved via the simplified constructor at runtime (currently only used in tests, but the entry exists), threshold tuning in config is silently ignored.

**Fix:** Either delete the simplified ctor (DI always supplies all services) or make fallbacks read the same Options singleton.

---

## POSITIVE

- **Tenant isolation maintained:** every product lookup switched from `GetProductByCodeAsync` (no tenant) to `GetActiveProductByCodeAsync` (tenant-scoped). Good catch.
- **Async PolicyGuard signature:** transition from sync `Evaluate(message)` to async `EvaluateAsync(PolicyGuardRequest)` is correctly threaded through both the base class and CompleteStateHandler.
- **Fact-validation rollout:** new `BuildFactValidationContext` helper (line 1363) eliminates duplicated `ResponseValidationContext` construction across 4 call sites. Strong DRY win.
- **Numbered suggestion selection:** `ExtractRelatedSuggestionSelectionNumber` regex carefully avoids false positives like "tôi muốn 1 cái" (line 2369-2380). Comments explain the intent — well done.
- **History sanitization:** wiring `SanitizeAssistantHistory` into AI ambiguity resolution (line 1851) plugs a known hallucination vector where prior assistant turns mentioned products outside the allowed set.
- **`LegacyFalseFactStateHandlerTests`:** new tests verify tenant isolation in product/variant/browsing/skin handlers — exactly the multi-tenant concern called out in `CLAUDE.md`.
- **Configurable `AiDetectionTimeoutMs: 500`:** good operational lever added to SalesBot options.

---

## Recommended Actions

1. **(Critical)** Fix duplicate-user-message bug in `CompleteStateHandler.HandleInternalAsync` — extract or call `BuildPolicyGuardRequest` from base.
2. **(Critical)** Fix `TestSalesStateHandler` constructors to forward `subIntentClassifier` (lines 3164, 3210) instead of replacing with mock.
3. **(Critical)** Add at least 3 tests for SubIntent integration (called when Consulting, not called otherwise, guidance forwarded).
4. **(Important)** Pass `ConversationContext` and a `CancellationToken` to `SubIntentClassifier.ClassifyAsync` at line 370.
5. **(Important)** Decide SubIntent vs hardcoded question routers — currently both run, SubIntent mostly wasted.
6. **(Important)** Plumb `CancellationToken` through `IStateHandler.HandleAsync`. Mechanical refactor; touch all handlers once.
7. **(Important)** Modularize `SalesStateHandlerBase` — at minimum extract routing decision and CTA policy into named services.
8. **(Minor)** Delete dead `RequiresProductGrounding(string)` overload.
9. **(Minor)** Drop `_productGroundingService` constructor fallback; require DI.
10. **(Minor)** Add changelog note for `SalesBot:EscalationKeywords` removal.

## Metrics

- Files changed: 15
- LOC added/removed: +2490 / -111
- Test files: 4 changed (1 new)
- Test methods added: ~20 (estimated from diff context — none cover SubIntent integration)
- Production files >200 LOC after change: SalesStateHandlerBase = 2,793 (target: <200)
- Cancellation token coverage in changed code: 0/11 await points
- SubIntent integration test coverage: 0%

## Unresolved Questions

1. Is `IStateHandler.HandleAsync` signature changeable to accept `CancellationToken`, or is it locked by an external contract?
2. Was the A/B test (control vs treatment) deliberately designed to bundle pipeline + SubIntent into one variable, or was that an oversight?
3. Is StateContext currently in-memory only, or is there a Redis/DB persistence layer that would round-trip `SubIntentResult` through JSON? (Determines severity of #7.)
4. Are there production config files setting `SalesBot:EscalationKeywords` that need migration, or only the example/default config?
5. Should the canned question routers (`BuildPriceConsultationReplyAsync` etc.) be deprecated in favor of SubIntent-driven routing, or should they continue as fast paths?

---

**Status:** DONE_WITH_CONCERNS
**Summary:** SubIntent feature ships untested at handler level (3 critical), with one real correctness bug in CompleteStateHandler's duplicate user-turn (#1). Refactor mostly works but bloats an already-monolithic base class to 2,793 LOC and propagates `CancellationToken.None` everywhere. Recommend addressing #1, #2, #3 before merge; remaining items can ship as follow-up.
