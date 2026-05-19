# Branch Review Synthesis — feat/subintent-classification-system

**Date:** 2026-05-07 22:20
**Branch:** feat/subintent-classification-system
**Range:** master..HEAD (8090cc7e..6b47dcd8)
**Scope:** 69 files, +6594/-111 LOC (1 commit)
**Reviewers:** 4 parallel code-reviewer agents (SubIntent, Policy, ProductGrounding, Handlers) + adversarial sweep

## Verdict

**DO NOT MERGE.** 7 CRITICAL findings, 3 of which break production at first request.

Tests pass (build OK, 30 pre-existing nullable warnings, 0 errors). All test green status is misleading — unit tests use mocks, integration tests REPLACE the broken DI registration. Production wiring fails on cold start.

---

## CRITICAL — Block merge

### C1. SubIntent DI registration broken (production crash)
**File:** `src/MessengerWebhook/Program.cs:302`

```csharp
builder.Services.AddScoped<MessengerWebhook.Services.SubIntent.GeminiSubIntentClassifier>();
```

Should be `AddHttpClient<GeminiSubIntentClassifier>()`. ASP.NET Core does not register raw `HttpClient` in DI by default. First request that resolves `SalesStateHandlerBase` → `HybridSubIntentClassifier` → `GeminiSubIntentClassifier` will throw `InvalidOperationException: Unable to resolve service for type 'System.Net.Http.HttpClient'`.

Why tests pass:
- Unit tests inject `Mock.Of<ISubIntentClassifier>()` (skip the dependency chain).
- Integration tests `services.RemoveAll<ISubIntentClassifier>()` then add `TestSubIntentClassifier` (`CustomWebApplicationFactory.cs:118-123`).

Production traffic was never exercised against the real DI graph.

### C2. HttpClient.BaseAddress not configured
Tied to C1: even if `AddHttpClient<>` were used, no `client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/")` is set. `GeminiSubIntentClassifier:50` issues relative `v1beta/models/...` URL → `InvalidOperationException` at first call.

Compare Program.cs:431 (Policy classifier — done correctly) and Program.cs:420 (GeminiService — done correctly).

### C3. Gemini API key in URL query string (log leak)
**Files:**
- `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs:52-53`
- `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs:45-48`

```csharp
url += $"?key={Uri.EscapeDataString(_geminiOptions.ApiKey)}";
```

API key ends up in HTTP access logs, proxy logs, OpenTelemetry traces, error stack traces. Codebase already uses `GeminiAuthHandler` with header auth for `IGeminiService` — these two new classifiers diverged from the convention.

Fix: header `x-goog-api-key: {apiKey}` via `HttpRequestMessage.Headers` or reuse `GeminiAuthHandler` as `AddHttpMessageHandler`.

### C4. ProductGrounding normalizer mismatch (hallucination leak)
**Files:**
- `Services/ProductGrounding/ProductGroundingService.cs:219-222` — collapses whitespace only
- `Services/ResponseValidation/ResponseValidationService.cs:253-269` — strips diacritics + maps `đ→d`

Sanitizer and validator disagree on equivalence. An assistant message containing `Mat na duong am` (no accents) can be filtered by one layer while passing the other (or vice versa). Direct violation of spec acceptance #5 in `docs/superpowers/specs/2026-04-26-product-grounding-hallucination-fix-design.md`.

The Tảo Biển hallucination incident specifically involves accented Vietnamese product names — this divergence will manifest there.

Fix: extract a shared `ProductNameNormalizer` utility used by both layers. Single source of truth for diacritic stripping + đ→d + space collapse.

### C5. Duplicate user message in CompleteStateHandler PolicyGuard input
**File:** `src/MessengerWebhook/StateMachine/Handlers/CompleteStateHandler.cs:139-149`

Handler calls `AddToHistory(userMessage)` then constructs `recentTurns` re-including the same message. Combined with `PolicyGuard.RepeatMentionBoost = 0.10` (default), borderline messages get +0.10 boost from a phantom repeat. Base class has dedupe guard at `SalesStateHandlerBase.cs:738-743` — this handler reimplemented the logic incorrectly.

Real-world impact: false escalations to human handoff for innocuous messages.

### C6. Zero handler-level tests for SubIntent
All 43 modified handler tests inject `Mock.Of<ISubIntentClassifier>()`. No test asserts:
- Classification actually fires
- Guidance is forwarded into Gemini prompt
- Sub-intent affects routing/response shape

The headline feature of this branch has zero behavioral coverage at the integration layer. Unit tests cover `KeywordSubIntentDetector`, `GeminiSubIntentClassifier`, `HybridSubIntentClassifier` in isolation — none verify they wire up correctly when used by a state handler.

### C7. TestSalesStateHandler discards SubIntent constructor param
**File:** `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs:3164, 3210`

Test helper accepts `ISubIntentClassifier subIntentClassifier` then ignores it, passing `Mock.Of<ISubIntentClassifier>()` to the base. Future test setup mistakes silently pass.

---

## IMPORTANT — Fix before next merge cycle

### I1. PolicyGuard signal score double-counting
`PolicyGuardService.cs:72-76` dedup key includes `Detector` field, but each detector reports a different Detector value, so for input "hoan tien" all 3 detectors fire with weights 0.55+0.45+0.20=1.20 (capped at 1.0). The dedup intent is broken; risk scorer sums duplicates.

Fix: dedup key `{MatchedText, Reason}` only, or take max-weight-per-Reason.

### I2. PolicyGuard classifier missing resilience handler
`Program.cs:431` `AddHttpClient<IPolicyIntentClassifier, GeminiPolicyIntentClassifier>()` — no `AddStandardResilienceHandler` or `AddHttpMessageHandler<GeminiRetryHandler>`. Compare line 420 (`IGeminiService` has retry). Transient 429/503 → silent classifier downgrade → inconsistent gating.

### I3. CountRepeatMentions direction likely reversed
`DefaultPolicyRiskScorer.cs:108-122` checks `request.Message.Contains(turn.Content, ...)`. For short turns ("ok", "yes") this matches every recent turn whose content is a substring of current message. Likely wanted the reverse direction (user repeating same input).

### I4. SubIntent keyword tie-breaking non-deterministic
`KeywordSubIntentDetector` uses Dictionary enumeration order. Phrase overlap (e.g., `"khuyến mãi"` vs `"khuyến mãi kèm"`, `"có gì"` substring traps) produces non-deterministic category selection across runs.

### I5. Cancellation tokens dropped at 11 await points in handler base
`SalesStateHandlerBase` passes `CancellationToken.None` to async dependencies despite receiving `cancellationToken` at the entry. Long-running Gemini calls cannot be cancelled when client disconnects.

### I6. SubIntent classified but ignored when keyword routers fire first
Some handler paths run keyword routing before sub-intent guidance is consulted. Sub-intent then runs and is logged but doesn't affect response. Wasted classifier cost + misleading telemetry.

### I7. Hybrid `Source = "hybrid"` overwrites accurate `"ai"` value
`HybridSubIntentClassifier.cs:68` `aiResult with { Source = "hybrid" }`. Breaks observability — telemetry can no longer distinguish AI-fallback decisions from keyword decisions.

### I8. SubIntent unit tests cover happy paths only
Missing: AI confidence in `[0.5, 0.7)` keyword fallback path, both-null path, AI/keyword category disagreement, cancellation propagation, AI throw exceptions.

### I9. ProductMentionDetector regex over-captures
- I9a: captures across Vietnamese conjunctions `và`/`hoặc`
- I9b: leading-word strip runs only once
- I9c: bare `?` triggers grounding too aggressively
- I9d: single digit triggers brand-like signal (`kem 30k` flagged)

### I10. Spec acceptance regression test missing
Spec acceptance scenario (customer says `mặt nạ dưỡng ẩm`, Gemini emits `Mặt nạ Tảo Biển Tươi Múi Xù`, expect fallback) has no integration test. The exact incident the design was written to prevent has no automated guard.

### I11. SalesStateHandlerBase 2,793 LOC
Base class grew by 636 lines on this branch alone. Test file `SalesStateHandlerBaseTests` grew +1448 lines. Smell: handler logic accumulating in the base; no clear extraction boundary. Modularization candidate per project rules (`<200 LOC/file` guideline).

### I12. Brittle JSON extraction in both Gemini classifiers
`ExtractJson()` strips ` ```json` markdown fences manually. Fragile if response shape changes. Fix: use Gemini structured output (`responseMimeType: "application/json"` + schema). Eliminates the entire helper.

---

## MINOR

- `EnsureClosingCallToAction` dead method on `IPolicyGuardService` (Policy YAGNI)
- `Evaluate(string)` sync overload in `PolicyGuardService` keeps two divergent code paths
- `ValidatePolicyGuardOptions` parameterless ctor leaks into tests, masks DI dependency
- Risk scorer `Math.Max(confidence, 1m)` — misleading; just write `1m`
- `ShouldAttemptSemanticClassifier` trigger phrases too broad ("manual", "support" loanwords)
- `KeywordPolicySignalDetector` weight-vs-priority logic underdocumented
- `PolicyDecision.Signals` defaults to `null`, type allows null vs always-list contract
- No input length cap before classifier prompt → 100KB message → 25K tokens cost
- `RequiresProductGrounding(string)` legacy overload unused
- `SalesHandlerFallbacks.PolicyGuardService` static singleton bypasses configured options
- A/B variant divergence beyond SubIntent (handlers diverge in unrelated ways)
- `SalesBot:EscalationKeywords` config silently removed (no doc/changelog)

---

## POSITIVE

- Hybrid keyword-first + AI fallback architecture is sound; cost/latency tradeoff well-reasoned
- `EnableSemanticClassifier=false` default — opt-in AI feature, safe rollout
- Vietnamese normalization (`FormD` decomposition + `NonSpacingMark` strip + leetspeak) correct in `DefaultPolicyMessageNormalizer`
- Policy threshold validation (`SafeReply ≤ Soft ≤ Hard`) enforced via `ValidatePolicyGuardOptions`
- Fail-closed classifier: exceptions fall back to deterministic signals (test verifies)
- Cancellation distinguishes user-cancel from timeout in `GeminiPolicyIntentClassifier`
- `HasHardEscalationSignal` skips classifier when deterministic signals already escalate (prevents hostile classifier from de-escalating)
- Tenant isolation tightened (`GetActiveProductByCodeAsync` replaces `GetProductByCodeAsync`)
- `BuildFactValidationContext` DRY consolidation in handler base
- ProductGrounding wired in 4 entry points consistently with single fallback constant
- New `LegacyFalseFactStateHandlerTests` verify multi-tenant scope
- Detector/normalizer/scorer interface separation enables clean unit testing
- Test discipline: Policy module has 13+12 tests for service+classifier with real behavior assertions

---

## Recommended Fix Order

1. **C1 + C2** — Add `AddHttpClient<GeminiSubIntentClassifier>(c => c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"))` or refactor to inherit Gemini HTTP setup. Verify with a real integration test that does NOT replace the DI registration.
2. **C3** — Move API key to `x-goog-api-key` header in both Gemini classifiers. Reuse `GeminiAuthHandler`.
3. **C4** — Extract shared `ProductNameNormalizer` utility, replace both call sites.
4. **C5** — Remove duplicate `recentTurns` append in `CompleteStateHandler.cs:139-149`. Or delete this handler's local construction and use the base helper.
5. **C6 + C7** — Add at least 3 handler-integration tests that exercise the real `HybridSubIntentClassifier` (with mocked HttpMessageHandler returning canned Gemini responses) — verify SubIntent affects prompt/response. Fix the test helper to stop discarding the parameter.
6. **C8 (regression test)** — Add integration test for `mặt nạ dưỡng ẩm` → fake-Gemini-emits-Tảo-Biển → expect fallback. Pin the exact spec scenario.
7. Address Important findings in subsequent commits.

---

## Per-module reviewer reports

Detailed findings live in:
- `plans/reports/code-reviewer-260507-2210-subintent-review.md`
- `plans/reports/code-reviewer-260507-2210-product-grounding-review.md`
- `plans/reports/code-reviewer-260507-2210-handlers-review.md`
- Policy review: returned inline (agent did not write to disk despite instructions; findings captured in this synthesis)

---

## Unresolved Questions

1. Was per-detector summation in Policy risk scoring intentional ("more detectors = more confident") or a dedup bug? Existing tests don't pin behavior — confirm with author.
2. Should `SalesHandlerFallbacks.PolicyGuardService` static singleton be removed entirely? It bypasses configured options and the new pipeline.
3. Why was `GeminiSubIntentClassifier` registered differently from `GeminiPolicyIntentClassifier` in the same branch? Was the `AddHttpClient<>` line accidentally dropped?
4. Spec phase 2 (structured product facts) is referenced in design doc but not in this branch. Is phase 2 explicitly out of scope for this PR?
5. `SalesBot:EscalationKeywords` config key was removed — replaced by what? Or genuinely dropped?
6. `EvaluateAsync` returns `ValueTask` while `IPolicyIntentClassifier.ClassifyAsync` returns `Task` — intentional or oversight?
