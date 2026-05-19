# SubIntent Classification Module - Code Review

**Branch:** `feat/subintent-classification-system`
**Diff range:** `master..HEAD` (8090cc7e..6b47dcd8)
**Reviewer:** code-reviewer (staff-engineer mode)
**Date:** 2026-05-07
**Scope:** ~1088 LoC across 11 files (8 production, 3 test files)

## Summary
Module is well-structured: clean interface, sensible hybrid pattern, decent test coverage of happy paths. **However it cannot run in production as registered** — DI for `GeminiSubIntentClassifier` will throw at first request. Several quality issues with non-determinism, weak test coverage of the actual hybrid logic, and brittle prompt parsing.

All 20 unit tests pass. Build succeeds. The DI bug is invisible in tests because tests instantiate classes directly.

---

## CRITICAL (blocks merge)

### C1. DI registration will throw at runtime — `GeminiSubIntentClassifier` has no HttpClient
**File:** `src/MessengerWebhook/Program.cs:302`

```csharp
builder.Services.AddScoped<MessengerWebhook.Services.SubIntent.GeminiSubIntentClassifier>();
```

`GeminiSubIntentClassifier` constructor takes `HttpClient` (line 23), but it is registered with `AddScoped`, not `AddHttpClient`. The DI container has no `HttpClient` service to inject — the only `HttpClient` registrations in `Program.cs` are typed (`AddHttpClient<VertexAIEmbeddingService>`, `AddHttpClient<IGeminiService, GeminiService>`, etc.). Each typed registration produces a *different* `HttpClient` instance scoped to that type only.

**Why it matters:** The first time `ISubIntentClassifier` is resolved (Consulting state, line 370 in `SalesStateHandlerBase`), DI will throw `InvalidOperationException: Unable to resolve service for type 'System.Net.Http.HttpClient'`. This is the exact path the feature is gated on. **Production crash on first sales conversation that hits Consulting state.**

The existing pattern in the codebase (compare with `IPolicyIntentClassifier` at line 431) is `AddHttpClient<TInterface, TImpl>()` with `BaseAddress` and `Timeout` configured.

**Why tests don't catch this:** All unit tests new up `GeminiSubIntentClassifier` with a hand-built `HttpClient`. There is no DI integration test that boots the host and resolves `ISubIntentClassifier`.

**Fix:**
```csharp
builder.Services.AddHttpClient<MessengerWebhook.Services.SubIntent.GeminiSubIntentClassifier>()
    .ConfigureHttpClient((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));
```

Also add a smoke test that resolves `ISubIntentClassifier` from a built `WebApplicationFactory` to prevent regressions.

---

### C2. `BaseAddress` is never set — request URL will be relative-only
**File:** `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs:50`

```csharp
var url = $"v1beta/models/{model}:generateContent";
...
var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, timeoutCts.Token);
```

The URL is relative. For this to work, the injected `HttpClient.BaseAddress` must be set to `https://generativelanguage.googleapis.com/`. Because of C1, this is never configured by DI. Even if C1 is fixed via `AddHttpClient<>` without explicit `BaseAddress`, the call will fail with `InvalidOperationException: An invalid request URI was provided`.

**Fix:** Configure `BaseAddress` in the `AddHttpClient` registration as part of the C1 fix. Tests already do this manually (line 28 in test).

---

### C3. API key leaked in request URL → logs/proxies/404 telemetry
**File:** `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs:51-54`

```csharp
if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
{
    url += $"?key={Uri.EscapeDataString(_geminiOptions.ApiKey)}";
}
```

API key is appended to request URL. Existing services in this codebase (e.g., `GeminiAuthHandler`) use the `x-goog-api-key` HTTP header (also seen in `SubIntentClassifierIntegrationTests.cs:41`). URLs end up in:
- Application logs (especially if any request middleware logs URLs)
- HTTP client diagnostic listeners / OpenTelemetry exporters
- Upstream reverse-proxy access logs
- `HttpRequestException` messages on failure
- 4xx error responses sometimes echo the path

**Fix:** Use the header (consistent with `GeminiAuthHandler`):
```csharp
var url = $"v1beta/models/{model}:generateContent";
using var req = new HttpRequestMessage(HttpMethod.Post, url)
{
    Content = JsonContent.Create(request, options: JsonOptions)
};
if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
    req.Headers.Add("x-goog-api-key", _geminiOptions.ApiKey);
var response = await _httpClient.SendAsync(req, timeoutCts.Token);
```

Or better, attach `GeminiAuthHandler` as a delegating handler in the `AddHttpClient` registration (like existing services do).

---

## IMPORTANT (must fix soon)

### I1. Keyword tie-breaking is non-deterministic — depends on Dictionary enumeration order
**File:** `src/MessengerWebhook/Services/SubIntent/KeywordSubIntentDetector.cs:101`

```csharp
var best = categoryScores.OrderByDescending(kvp => kvp.Value.MatchCount).First();
```

`OrderByDescending` is **stable but the input is `Dictionary<,>`** whose enumeration order is not part of its contract. When two categories have equal match counts (very plausible — see I2 below), which wins is implementation-defined and may change between .NET versions or after rehashing.

**Why it matters:** Customer message "khuyến mãi kèm có gì không?" hits PriceQuestion (substring `"khuyến mãi"`) AND PolicyQuestion (`"khuyến mãi kèm"`) with 1 match each. Production behavior may flip silently after a runtime upgrade.

**Fix:** Use ordered iteration with explicit tiebreak (longer matched keyword wins, then enum order):
```csharp
var best = categoryScores
    .OrderByDescending(kvp => kvp.Value.MatchCount)
    .ThenByDescending(kvp => kvp.Value.Matched.Max(k => k.Length))
    .ThenBy(kvp => (int)kvp.Key)
    .First();
```

---

### I2. Keyword overlaps create silent misclassification
**File:** `src/MessengerWebhook/Services/SubIntent/KeywordSubIntentDetector.cs:28, 43`

- PriceQuestion: `"khuyến mãi"` (line 28)
- PolicyQuestion: `"khuyến mãi kèm"`, `"tặng kèm"`, `"quà tặng"`, `"gift"`, `"bonus"` (line 43)
- PriceQuestion: `"sale"` (substring of "khuyến mãi kèm sale" type messages) — also matches `"sale"` literal in unrelated contexts
- ProductQuestion: `"có gì"` (line 17) — matches in "còn hàng có gì không?" along with AvailabilityQuestion `"còn hàng"`

Substring matching with overlapping vocab causes silent wrong-category routing (high keyword confidence skips AI, so this is not self-correcting).

The tests do not cover any of these collision cases. They only test single-category messages.

**Fix:** Either
1. Use word-boundary regex matching (`\b...\b` doesn't apply cleanly to Vietnamese without diacritic-aware tokenization, but `(^|\W)keyword(\W|$)` works), or
2. Sort each category's keywords longest-first and stop matching on first hit per category, or
3. Match with a single combined `Aho-Corasick`-style pass and pick longest match per position.

At minimum, add tests with multi-category-overlapping messages and document the resolution rule.

---

### I3. `CalculateConfidence` returns 0.95 for ≥3 matches — but ≥3 occurrences of the SAME keyword should not count
**File:** `src/MessengerWebhook/Services/SubIntent/KeywordSubIntentDetector.cs:81-94, 112-121`

Loop adds each keyword once per category if it occurs anywhere in the message. So `MatchCount` is "distinct keywords matched", which is fine. But `string.Contains` matches substrings: `"giá"` matches inside `"giảm giá"` AND `"giá"` AND `"giá bao nhiêu"` (already 3 distinct keywords from one short phrase "giá bao nhiêu giảm giá") → confidence locked to 0.95 with no AI fallback even though the user just asked one thing twice.

This is mostly fine but means short messages can hit 0.95 confidence trivially. The unit test `Detect_MultipleKeywords_ReturnsHighConfidence` (line 47-54) deliberately uses a multi-keyword message but does not establish that 0.95 is *only* reached when warranted.

**Fix:** Either deduplicate matches at "concept" level (group keywords by intent within each category) or keep current behavior but document it. Lower priority than I1/I2.

---

### I4. JSON markdown extractor is brittle and silently drops body text
**File:** `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs:185-206`

```csharp
private static string ExtractJson(string text)
{
    var trimmed = text.Trim();
    if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        return trimmed;
    var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
    if (end <= 3) return trimmed;
    var body = trimmed[3..end].Trim();
    if (body.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        body = body[4..].Trim();
    return body;
}
```

Issues:
1. If Gemini returns `Here is the answer:\n\`\`\`json\n{...}\n\`\`\`\nLet me know if...`, `LastIndexOf("\`\`\`")` finds the closing fence, but `[3..end]` strips only the **first 3 chars**, so the explanatory prefix `Here is the answer:` ends up inside the JSON parse → throws → caught generically → returns null silently. Customer query goes to fallback path with no visible error.
2. If the response has no fences, the trimmed text is fed straight to `Deserialize` even if it has prose. Same silent failure.
3. No tests for malformed JSON, prose-wrapped JSON, or `\`\`\`json{...}\`\`\`` (no newlines).

**Fix:** Use Gemini's `responseMimeType: "application/json"` in `generationConfig` (Flash-Lite supports it), eliminating the need to extract:
```csharp
generationConfig = new
{
    temperature = 0.1,
    maxOutputTokens = 150,
    responseMimeType = "application/json"
}
```
Then `text` is guaranteed JSON. Keep `ExtractJson` as defensive fallback but log a warning when fences are detected.

Also add test cases for: prose-wrapped JSON, extra trailing text, raw `{...}`.

---

### I5. Unit tests do not verify the documented "0.5 fallback safety" or AI-low-confidence-then-keyword path
**File:** `tests/MessengerWebhook.UnitTests/Services/SubIntent/HybridSubIntentClassifierTests.cs`

Spec says: AI must clear `HybridAiAcceptanceThreshold` (0.7) to win; otherwise fall back to the keyword result. The test `ClassifyAsync_AiFails_FallsBackToKeyword` only covers the *exception* path. There is no test where AI returns successfully with confidence between `MinConfidence` (0.5) and `HybridAiAcceptanceThreshold` (0.7), proving keyword wins.

There is also no test for:
- AI returns success with confidence in `[0.5, 0.7)` — keyword fallback path (covers HybridSubIntentClassifier.cs:60→72)
- Both keyword and AI return null (line 82-83)
- AI returns DIFFERENT category than keyword at high confidence — should the keyword be discarded? (Currently: yes, AI wins. Verify intentional.)
- Cancellation propagation — passing a pre-canceled token must not log "timed out" (line 96 swallows it because `cancellationToken.IsCancellationRequested` is true, but if the *outer* token is canceled mid-flight the warning fires inappropriately).

The `ClassifyAsync_KeywordHighConfidence_SkipsAi` test verifies HTTP was not called, but the message `"giá bao nhiêu tiền?"` only has 2 keyword matches (`"giá"`, `"bao nhiêu"`, `"tiền"` → 3) which gives 0.95 — this is fine, but the test does not assert `Source == "keyword"` (it asserts the wrong invariant: confidence ≥ 0.9 includes AI results too).

**Fix:** Add the missing tests. They are quick to write since the harness is in place.

---

### I6. Cancellation token semantics in the timeout catch block are subtly wrong
**File:** `src/MessengerWebhook/Services/SubIntent/GeminiSubIntentClassifier.cs:96-101`

```csharp
catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    _logger.LogWarning("Gemini sub-intent classifier timed out after {TimeoutMs}ms",
        _subIntentOptions.ClassifierTimeoutMs);
    return null;
}
```

The filter `!cancellationToken.IsCancellationRequested` is meant to distinguish "internal timeout" from "caller-canceled". But there is a race: if the outer token is canceled *during* the HTTP call (e.g., request abort), `OperationCanceledException` is thrown AND `cancellationToken.IsCancellationRequested == true`. The filter then evaluates false → exception bubbles up unhandled, ultimately caught by the broad `catch (Exception ex)` below, logged as a generic failure, and returns null. Functionally OK, but the semantics in the outer caller (`HybridSubIntentClassifier`) are wrong: a caller-side cancellation should propagate, not become a swallowed null that triggers keyword fallback.

**Fix:**
```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    throw; // honor outer cancellation
}
catch (OperationCanceledException)
{
    _logger.LogWarning("Gemini sub-intent classifier timed out after {TimeoutMs}ms", _subIntentOptions.ClassifierTimeoutMs);
    return null;
}
```

And in `HybridSubIntentClassifier.ClassifyAsync`, do not "fall back to keyword" if the outer token was canceled — propagate cancellation up.

---

### I7. Confidence thresholds: keyword `0.65` (1 match) is below `KeywordHighConfidenceThreshold 0.9` AND below AI threshold `0.7` — so single-keyword matches always escalate to AI. Documented behavior says ~70% short-circuit at keyword. With current scoring, only messages with ≥2 keyword matches short-circuit.

**File:** `KeywordSubIntentDetector.cs:112-121`

If most production messages have 1 keyword (likely for short Vietnamese queries like "giá?" → 1 match → 0.65), keyword path almost never short-circuits → AI is hit ~every time → cost balloons + latency degrades from "<500ms 70% of queries" promise to "~1s ~always".

**Fix:** Reconsider scoring or threshold. Either bump 1-match confidence to 0.9 for *unambiguous* keywords (e.g., `"freeship"`, `"sale"`, `"refund"`) or lower `KeywordHighConfidenceThreshold` to 0.65 for keywords flagged as high-precision. Validate against real conversation data before shipping.

---

### I8. `HybridSubIntentClassifier.cs:68` — `aiResult with { Source = "hybrid" }` overwrites accurate `"ai"` source
**File:** `src/MessengerWebhook/Services/SubIntent/HybridSubIntentClassifier.cs:68`

The Source field is meant to indicate *origin* (per spec: `keyword | ai | hybrid`). The hybrid path always returns `"hybrid"` regardless of whether AI or keyword "won", which destroys observability:
- AI accepted: returns Source="hybrid" (was "ai")
- AI low-conf, keyword fallback: returns Source="keyword"
- Keyword high-conf short-circuit: returns Source="keyword"

So `"hybrid"` actually means "AI won via hybrid path". This is confusing and metrics dashboards will have to reverse-engineer it.

**Fix:** Either:
1. Keep `"ai"` source on AI wins, only use `"hybrid"` for blended results (but the code never blends — it picks one).
2. Or add a separate `Path` field: `Source` stays accurate (keyword/ai), `Path` describes routing (fast/escalated/blended).

---

## MINOR (nice to have)

### M1. `ConversationContext.RecentHistory` is `List<>` not read-only
**File:** `src/MessengerWebhook/Services/SubIntent/ISubIntentClassifier.cs:33`

Public mutable list on a record. Consumers can mutate it after construction. Use `IReadOnlyList<ConversationMessage>` and `init`.

### M2. `SubIntentResult.MatchedKeywords` defaults to `Array.Empty<>()` — but factory `Create()` re-checks for null
The `record` already has the default; `Create` adding `?? Array.Empty<string>()` is redundant. (`SubIntentResult.cs:15, 41`)

### M3. Magic-string `"keyword" | "ai" | "hybrid"` for Source
Should be a const or an enum. Risk of typo silently breaking metrics filters.

### M4. `BuildHistoryContext` does not bound message length
**File:** `GeminiSubIntentClassifier.cs:178-183`

`TakeLast(3)` limits count but each message could be 5 KB. Token blowup possible. Add per-message char cap (e.g., 200 chars).

### M5. Prompt has Vietnamese examples but instruction "brief explanation in English" — mixed locale
**File:** `GeminiSubIntentClassifier.cs:165`

If observability/metrics are in Vietnamese context, force consistent locale. Low impact.

### M6. Keyword list is `static readonly Dictionary<>` — fine for read-only, but case is not normalized at definition time
Keywords already lowercase; document the invariant or call `.ToLowerInvariant()` once at static-ctor time as defense-in-depth.

### M7. Integration test class `IDisposable` but `_httpClient.Dispose()` should be in pattern with finalizer suppression
**File:** `SubIntentClassifierIntegrationTests.cs:94-97` — Minor xUnit hygiene; the pattern is fine for test classes.

### M8. `GeminiOptions.FlashLiteModel` default is `"gemini-1.5-flash"` (not Flash-Lite as comment claims)
**File:** `Configuration/GeminiOptions.cs:9` (pre-existing, not from this diff) — but worth flagging since this is the model the SubIntent classifier uses.

### M9. Test naming: `Detect_*` prefix used in `KeywordSubIntentDetectorTests` but methods call `ClassifyAsync` — stale rename artifact
File: `KeywordSubIntentDetectorTests.cs` — Cosmetic.

---

## POSITIVE

- **Clean separation of concerns:** keyword/AI/hybrid as three classes implementing one interface — easy to mock, easy to swap.
- **Sensible defaults in `SubIntentOptions`:** explicit thresholds with XML doc comments, all configurable.
- **Per-call timeout via `CancellationTokenSource.CreateLinkedTokenSource`** — correct pattern.
- **Generic exception swallow with logging** in AI classifier (line 102-106) — appropriate fail-open behavior since this is a non-critical classification, downstream code handles `null`.
- **Few-shot examples in prompt** — good prompt engineering, includes negative example ("ok" → none).
- **Confidence as `decimal`** rather than `double` — appropriate for boundary comparisons.
- **`SubIntentResult.Create` factory validates confidence range** — defensive.
- **Tests use `FluentAssertions`** consistent with project style.
- **`Moq.Protected` for HttpMessageHandler mocking** — proper unit test isolation.
- **AI classifier registered as `Scoped`** — appropriate for per-request HttpClient consumption (once C1 is fixed).

---

## Recommended Action Order

1. **C1 + C2** — fix DI registration to `AddHttpClient<>` with `BaseAddress`, run smoke test that resolves `ISubIntentClassifier` from DI.
2. **C3** — move API key from URL query to header (or via `GeminiAuthHandler`).
3. **I1, I2** — fix non-deterministic tie-break and keyword overlaps.
4. **I4** — switch to `responseMimeType: application/json` in Gemini config.
5. **I5** — add the four missing test cases.
6. **I6** — fix cancellation token filter logic.
7. **I7** — validate confidence thresholds against real query distribution before merging.
8. **I8** — clarify Source field semantics (or add Path field).
9. M-series after.

---

## Metrics

- **Type Coverage:** 100% (no `dynamic`, no `object` returns)
- **Test Coverage:** ~60% of behavioral surface (happy paths only; missing AI-low-conf-keyword-wins, both-null, cross-token-cancellation, malformed-JSON, tie-break)
- **Linting Issues:** 0 new (pre-existing warnings in unrelated files)
- **Build:** succeeds (.NET 8 prod, .NET 9 tests)
- **Tests passing:** 20/20 SubIntent unit tests

---

## Unresolved Questions

1. **Was the AI key intended in the URL?** All other Gemini integrations in this repo use `x-goog-api-key` header. Confirm intent — possibly copy-paste from an older example.
2. **Is `"hybrid"` Source meant to indicate "AI won" or "blended"?** No blending logic exists — clarify semantics.
3. **What is the production keyword/AI ratio target?** Spec says 70/30. With current `0.65` confidence for 1 match, ratio will likely flip to AI-dominant. Has this been measured against real Vietnamese conversation logs?
4. **Should outer cancellation propagate or be swallowed?** Current `HybridSubIntentClassifier` treats AI cancellation identically to AI failure (falls back to keyword). For request-abort scenarios, this masks the abort.
5. **Is the integration test suite expected to run in CI?** All three tests are `Skip = "Integration test - requires valid Gemini API key"` — confirm whether a CI job sets the key (otherwise these are dead code).

---

**Status:** DONE_WITH_CONCERNS
**Summary:** Module logic is reasonable but ships with a runtime DI failure (C1/C2), an API-key-in-URL leak (C3), and silent non-determinism in keyword overlap resolution (I1/I2). All three Critical items must be fixed before merge. Tests pass but only cover happy paths — the hybrid fallback logic claimed by the spec is not actually verified.
**Concerns/Blockers:** C1/C2/C3 block merge. I1-I8 should be addressed in same PR or follow-up before this code is enabled in production.
