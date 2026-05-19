# Product Grounding Module Review

Diff range: master..HEAD (BASE_SHA=8090cc7e, HEAD_SHA=6b47dcd8)

Spec: docs/superpowers/specs/2026-04-26-product-grounding-hallucination-fix-design.md

## Summary

Phase 1 implemented and wired into multiple call sites (`SalesStateHandlerBase` lines 317, 683, 925, 1309, 1934). All 5 spec acceptance criteria appear satisfied at code level, but there is one **concrete correctness bug** (accent-insensitivity inconsistency between two normalizers) that will cause both false positives (real products filtered from history) and false negatives (hallucinated names slipping through history sanitization). Several additional regex over-capture risks remain.

---

## CRITICAL

### C1. Accent-normalization inconsistency between sanitizer and validator

**Files:**
- `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs:219-222`
- `src/MessengerWebhook/Services/ResponseValidation/ResponseValidationService.cs:253-269`

**Problem:**
`ProductGroundingService.NormalizeProductName` only collapses whitespace:

```csharp
return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
```

But `ResponseValidationService.NormalizeProductName` strips diacritics via `FormD` + `NonSpacingMark` filter and maps `đ→d`. Because `ProductGroundingService.SanitizeAssistantHistory` calls the LOCAL (non-accent-stripping) `ContainsEquivalent`, the same product name comparison yields different results in two layers.

**Why it's critical:**
- Bot legitimately replies "Mặt nạ Cấp Ẩm Rau Má (MN01)" → assistant turn saved.
- Allowed list contains exact `Mặt nạ Cấp Ẩm Rau Má` → validation passes.
- Next turn with sanitization: assistant content uses spelling variant or diacritic drift (e.g., Gemini emits "Mat na Cap Am Rau Ma" because of low-resource Vietnamese tokenization). Validator passes (accent-insensitive); sanitizer DROPS this verified turn from Gemini history because `NormalizeProductName` did not strip accents.
- Conversely, a hallucinated unaccented variant of an allowed name would NOT be filtered if it does not equal the allowed name once both are run through the weak normalizer.
- The two layers must agree, otherwise history fed to Gemini diverges from what the validator considers the truth.

**Fix:**
Replace the local `NormalizeProductName` in `ProductGroundingService.cs` with the same `FormD` + `NonSpacingMark` + `đ→d` logic used in `ResponseValidationService`. Better, extract one shared `ProductNameNormalizer` helper and use it from both services. (DRY violation today.)

```csharp
private static string NormalizeProductName(string value)
{
    var lowered = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder(lowered.Length);
    foreach (var ch in lowered)
    {
        if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            sb.Append(ch == 'đ' ? 'd' : ch);
    }
    return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
```

No test covers this divergence — add one: assistant turn says "Mat na Tao Bien Tuoi Mui Xu", allowed list contains accented form, expect sanitizer to TREAT IT as the allowed product (i.e., do NOT filter).

---

## IMPORTANT

### I1. `ProductMentionDetector` over-captures across "and"/multi-product enumerations

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductMentionDetector.cs:159`

**Problem:**
`CategoryProductNameRegex` greedy class `[\p{L}\p{M}0-9\s\-\.]*` consumes everything until punctuation, including conjunctions and other product names. Example: `mặt nạ Tảo Biển và serum Vitamin C tốt cho da` captures `mặt nạ Tảo Biển và serum Vitamin C tốt cho da` as a single mention. After stop-word trimming on " tốt", you get `mặt nạ Tảo Biển và serum Vitamin C` — passes `IsSpecificProductMention` (3+ uppercase tokens) and is checked as a single product name against allowed list. Result: legitimate dual-product reply gets blocked even if BOTH products are allowed individually.

**Why:** Regex was designed without a Vietnamese conjunction stop list. Spec calls this out explicitly (line 178: "Regex product mention detection may over-capture long Vietnamese phrases").

**Fix:**
Add Vietnamese delimiters/conjunctions to stop-word list: ` và`, ` hoặc`, ` voi`, ` cùng`, ` và cả`, ` rồi`, ` xong`. Or restrict the regex character class to forbid running across more than ~6 words.

No test covers multi-product enumeration. Add one.

### I2. `ProductMentionDetector.RemoveLeadingWords` only strips ONE leading word

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductMentionDetector.cs:107-118`

**Problem:**
After `LeadingWords` strip, candidate may still start with another leading word. Example: `Dạ bên em có mặt nạ Tảo Biển ...` → first regex match candidate is `mặt nạ Tảo Biển ...` (leading word stripping not relevant), but for the contextual regex matches that capture phrase like `dạ bên em có ...`, only "dạ " gets stripped on first pass; "bên" remains. Function exits after first match. Net effect: candidates retain non-product leading tokens, contaminating equality match.

**Fix:** Loop until no leading word matches.

### I3. `ProductGroundingService.IsAllowedAssistantMessage` allows messages with zero detected mentions

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs:184-188`

**Problem:**
```csharp
return mentions.Count == 0 || mentions.All(m => IsAllowedMention(m, allowedProducts));
```

If `ProductMentionDetector` under-captures (e.g., a hallucinated lower-case 3-word product name without brand-like signals does not pass `IsSpecificProductMention`), the assistant turn is treated as "no mentions" → kept in history → fed back to Gemini → bot reinforces the hallucination on next turn. The whole defense pipeline depends on the detector's recall.

**Why important:** Spec line 91 acceptance: "Assistant history sent to Gemini does not contain unverified hallucinated product names." A weak detector silently violates this without anyone noticing (no error, just bad data flowing).

**Fix:** Pair the mention-based detector with a second-line defense: when `RequiresGrounding && !HasAllowedProducts`, the sanitizer should drop ALL prior assistant turns containing 3+ consecutive title-case Vietnamese tokens, not only those matched by the named-product regex. Or log every assistant message that bypasses sanitization with no mentions, so under-capture is observable.

### I4. `ProductNeedDetector` triggers on bare `?` for any message containing a product term

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductNeedDetector.cs:52`

**Problem:**
`message.Contains('?')` is the OR-fallback alongside need/catalog/fact terms. So "kem này là gì?" or "kem có không?" requires grounding. Combined with "no allowed products" path, this means a customer asking general clarification about an off-context product term gets the fallback message even when they asked something innocuous. This causes user-experience regressions.

**Why important:** Spec acknowledges conservative fallback (mitigation line 177), but a `?` is too broad. Consider gating: `?` triggers grounding only if there is also a need term OR no active product yet.

### I5. `ContainsBrandLikeSignal` false-positive on numeric-only suffix

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductMentionDetector.cs:131-136`

**Problem:**
`candidate.Any(char.IsDigit)` returns true for any digit, e.g., "kem 30k" or "serum 50ml" → flagged as specific product mention. After stop-word trimming, the candidate may be `kem 30k` (4-char). Will be checked against allowed list → fail → report ungrounded product. False positive could trip validation and replace a legitimate price-mention reply with the fallback.

**Fix:** Require digits only when accompanied by 2+ tokens or a hyphen, not standalone numerics. Or whitelist common units (k, ml, g) before counting.

---

## MINOR

### M1. Duplicate static `ProductMentionDetector` instance

**File:** `src/MessengerWebhook/Services/ResponseValidation/ResponseValidationService.cs:18`

`new ProductMentionDetector()` instantiated as static field while DI also registers a singleton. Two instances; harmless because state-free, but inconsistent. Either inject via constructor or expose pure static methods.

### M2. `RelatedTermGroups` "kem" matches too broadly

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs:34`

`new("kem", ..., new[] { "kem" })` matches the literal substring "kem" anywhere — e.g., the customer's name "Kem" or "kem đánh răng" (toothpaste, not in cosmetics catalog). May produce noisy related-product suggestions.

### M3. `ProductNeedDetector.NeedTerms` includes lone "khô" / "kho"

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductNeedDetector.cs:18`

"khô" / "kho" can match "không" (negation) or "kho" (warehouse) substrings via `Contains`. False positives on grounding requirement.

**Fix:** Word-boundary check via regex.

### M4. `BuildDeduplicationKey` tolerates whitespace-only Id by falling through to Code/Name

**File:** `src/MessengerWebhook/Services/ProductGrounding/ProductGroundingService.cs:229-242`

Uses `IsNullOrWhiteSpace`, OK. But if both Id and Code are whitespace, the Name key is used. If two products with empty Id/Code share a name, dedupe loses one. Probably never happens, but worth a comment or assertion.

### M5. Test gap: spec regression test missing

The spec's "Regression tests" (line 162-167) describe an end-to-end test: customer says `mặt nạ dưỡng ẩm`, Gemini emits `Mặt nạ Tảo Biển Tươi Múi Xù`, expect fallback. No such test exists in `tests/MessengerWebhook.UnitTests/Services/ProductGrounding/` or anywhere mocking Gemini. Only sub-component tests exist.

**Fix:** Add an integration-style test that runs `SalesStateHandlerBase.BuildNaturalReplyAsync` with a stubbed Gemini returning the hallucinated string and asserts response equals `ProductGroundingService.FallbackReply`.

---

## POSITIVE

- One consistent `FallbackReply` constant — single source of truth, all 9 call sites route through it (`BuildProductGroundingFallbackReply` or `groundingContext.FallbackReply`). Spec line 81-83 satisfied.
- `BuildContextWithRelatedSuggestionsAsync` adds value over the spec by offering RAG-DB-verified alternatives instead of a pure fallback. Validation re-runs on the related-suggestion reply (line 1217 `BuildGroundedRelatedSuggestionOrFallbackAsync`) — defense in depth.
- Sanitization wired in BEFORE every `SendMessageAsync` invocation (lines 931, 1315, 1934, 319). Spec line 91 acceptance satisfied at the call-site level.
- `ResponseValidationService.NormalizeProductName` correctly strips diacritics — appropriate for Vietnamese matching.
- Tests use real `ProductNeedDetector` and `ProductMentionDetector` in `ProductGroundingServiceTests`, not mocks → exercises actual regex behavior. Good practice.
- Test `BuildContextWithRelatedSuggestionsAsync_RelatedReply_PassesRealGroundingValidation` is excellent — verifies the related-suggestion reply round-trips through real `ResponseValidationService` (not just the service's own logic). Catches drift between the two layers for that scenario.
- Acceptance #1 (`mặt nạ dưỡng ẩm` requires grounding) is directly covered by `ProductNeedDetectorTests` InlineData.

---

## SPEC GAPS

Acceptance criteria status (spec lines 87-91):

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `mặt nạ dưỡng ẩm` requires grounding | ✅ PASS | `ProductNeedDetectorTests` InlineData |
| 2 | No allowed products → fallback before Gemini | ✅ PASS | `SalesStateHandlerBase.cs:925-928`, `:1310-1312` |
| 3 | Gemini name not in allowed → blocked | ✅ PASS | `ResponseValidationService.ValidateProductNames` |
| 4 | Active selected product / RAG names allowed | ✅ PASS | `BuildAllowedProducts` line 154 |
| 5 | Assistant history to Gemini has no unverified names | ⚠️ AT RISK | Wired in, but C1 (accent inconsistency) and I3 (under-capture silent pass) can bypass |

**Phase 1 spec gap not addressed:**
- Spec line 142-150 "Prompt changes" suggests adding explicit `ALLOWED PRODUCT NAMES:` block with rule "Nếu danh sách rỗng, không được gợi ý tên sản phẩm." Implementation only does `San pham duoc phep neu can neu ten: {list}` (line 1109, 1321). Missing the explicit "if empty, don't invent" instruction. The fallback short-circuit covers this case at code level, but the prompt itself is not hardened — if the short-circuit is ever bypassed, the prompt does not deter Gemini.

---

## Recommended Actions (priority order)

1. **C1** — Unify `NormalizeProductName` between `ProductGroundingService` and `ResponseValidationService`. Extract to shared helper. Add test for accented-vs-unaccented match parity.
2. **I3** — Add observability: log each assistant turn that bypasses sanitization while `RequiresGrounding && !HasAllowedProducts`. Helps detect under-capture in production.
3. **I1** — Add Vietnamese conjunctions/connectors to mention-detector trim list. Add test for multi-product enumeration.
4. **M5** — Add the spec regression test (Gemini stub → hallucinated name → assert fallback).
5. **I4** — Tighten `ProductNeedDetector` so bare `?` does not unilaterally trigger grounding.
6. **I2, I5, M2, M3** — Smaller correctness cleanups in detectors.
7. Update prompt template (line 1106-1121, 1318-1331) to include explicit "if list empty, do not invent product names" rule per spec line 144-150.

---

## Unresolved Questions

- Is `ProductGroundingService` intended to be invoked outside `SalesStateHandlerBase`? (e.g., `IdleStateHandler`, `HumanHandoffStateHandler` show in grep results — confirmed they reference but minimal). If so, sanitization may be missing on some paths.
- Should `BuildContext` (sync, no related suggestions) be deprecated in favor of `BuildContextWithRelatedSuggestionsAsync`? Current code uses both inconsistently (line 317 uses sync). The sync version cannot offer RAG fallbacks; consider whether intent-detection path should also benefit.
- Is there a metric/log already tracking how often the fallback fires? Spec phase 3 mentions observability — not in this diff.

---

**Status:** DONE_WITH_CONCERNS
**Summary:** Phase 1 functionally implements all 5 acceptance criteria, but a normalizer inconsistency (C1) silently breaks history sanitization for accented-vs-unaccented variants and several detector regex/heuristic risks (I1–I5) leave room for both over- and under-capture. Spec regression test missing.
**Concerns:** C1 is a real correctness bug to fix before relying on the hallucination guard for the live Tảo Biển incident.
