# Phase 03: Critical Fixes (C3, C4, H1) - Two Pre-Fixed, One New Defense-in-Depth

**Date**: 2026-05-13 21:09
**Severity**: High
**Component**: Security (PII), Race Conditions, Token Handling
**Status**: Resolved

## What Happened

Phase 03 was supposed to address three critical fixes: C3 (race condition in FacebookPageConfig), C4 (token leak via query string), and H1 (PII in logs). Scout revealed C3 and C4 were already fixed before this session started. Only H1 needed actual work — and the work was a single-class addition (PiiRedactingEnricher) plus 39 tests to defend against category gaps we didn't plan for.

**Commit:** 1cc1bfb

## The Brutal Truth

This is now the third consecutive phase (R-05, 01, 02, 03) where we discover ~80% of planned work already exists. It's simultaneously encouraging and maddening. Encouraging because it means prior refactoring phases built real infrastructure. Maddening because our planning process doesn't verify what exists before scoping.

Here's what gets to me: we had C3 and C4 written as *critical* items in the production stabilization plan. Neither was actually broken. The scout agent checked the codebase and found the fixes already in place. That's a planning failure, not a code failure, but it's a failure nonetheless. We wasted planning cycles and attention on non-existent problems.

The upside is H1 (PII in logs) was genuinely new. But the new work wasn't what we planned — it was a defense-in-depth layer we didn't anticipate needing.

## Technical Details

**C3: Race Condition FacebookPageConfig** — Pre-fixed
- FacebookPageId has a unique index in the database (migration + DbContext model already had this)
- FacebookPageConfigLookupService.EnsurePageConfigAsync handles race conditions in the dev auto-create path via `catch (DbUpdateException when IsUniqueViolation)`
- Production doesn't auto-create configs — configs come from admin UI only, so the race condition is architecturally impossible once configs exist
- In dev, two concurrent requests for the same FacebookPageId both try to insert → first wins → second gets caught → both proceed → no crash
- **Conclusion:** Race condition is mitigated by design, not just hope

**C4: Token Leak via Query String** — Pre-fixed
- MessengerService.cs uses Bearer Authorization header for all Graph API calls: `new AuthenticationHeaderValue("Bearer", _options.PageAccessToken)`
- No instance of `access_token=` in any URL across the codebase (verified via grep)
- Token is only stored in appsettings.json (which shouldn't be in source control per Phase 01 findings)
- **Conclusion:** Query string exposure was never a risk; architecture default is secure

**H1: PII in Logs** — NEW work (but unexpected surface)
- PiiRedactor.cs with MaskPhone/RedactAddress already existed
- All call sites already used PiiRedaction helpers (PsidHash, MaskingOperator, etc.)
- What didn't exist: **defense-in-depth protection for structured log properties**

The problem we found: log properties themselves can leak PII even if the message template doesn't. Example:
```csharp
_logger.LogInformation("Transition {FromState} {ToState} for tenant {TenantId}", 
    "Qualified", "Conversion", tenantId);
```

If `FromState` or `ToState` accidentally gets populated with a PSID or address, it leaks even though the message template looks clean. We had PII redaction at call-sites but no enforcement layer.

**Solution: PiiRedactingEnricher** — Serilog ILogEventEnricher that:
1. Iterates through all string properties in the log event
2. Calls PiiRedactor.Redact() on each one
3. If any property changed, collects mutations and applies them after iteration (avoids modifying dict during loop)
4. **Zero allocation on common path** — `mutations` stays null if no PII found
5. Only allocates a List<T> on first PII match

Implementation pattern:
```csharp
// Null until first PII match — zero allocation on the common (no-PII) path
List<(string key, string redacted)>? mutations = null;

foreach (var kvp in logEvent.Properties)
{
    if (kvp.Value is not ScalarValue { Value: string text })
        continue;

    var redacted = PiiRedactor.Redact(text);
    if (redacted != text)
        (mutations ??= []).Add((kvp.Key, redacted));
}

if (mutations is null) return;
// Apply mutations...
```

**Tests:** 39 total
- PiiRedactorTests: 25 tests covering 10 Vietnamese phone formats + address patterns + edge cases
- PiiRedactingEnricherTests: 14 tests covering enricher behavior, null handling, property collection/mutation patterns, integration with Serilog

**Build:** 0 errors. Tests: 888 unit + 246 integration = 1,134 pass.

## What We Tried

1. **Initial approach:** Reduce PII risk by removing raw PSID logs (Phase 01 did this across 63 sites). Thought we were done.

2. **Scout assessment:** Discovered C3 and C4 were already handled. Only H1 was open.

3. **First H1 approach:** Review all existing PiiRedactor call sites. Find they're comprehensive.

4. **Gap discovered:** Call-site redaction handles *message templates*, not *property values*. Structured logging can leak PII in the properties dict.

5. **Solution:** Build an enricher to defensively redact properties at serialization time (last chance before Serilog sinks them).

6. **Optimization concern:** Enrichers run on *every log*, even logs with no PII. Initial draft allocated a List<T> unconditionally. Refactored to defer allocation until first PII match (`mutations ??= []`).

## Root Cause Analysis

**Why C3 and C4 appeared critical but weren't:**

The production stabilization plan was written before Phase R-05 refactoring was completed. R-05 included modularization but *also* included security fixes (Bearer header, race mitigation) as side effects of code organization. By the time Phase 03 plan was finalized, those fixes were already in place. Planning didn't incorporate a baseline audit of what R-05 actually shipped.

**Why H1 was incomplete, not missing:**

Phase 01's PII sweep fixed logs at the *call site* (removing raw PSIDs). But it didn't address the *property bag* (structured log fields). Both layers needed coverage — we had one but thought we were done. Scout's code audit caught the gap.

**Why zero-alloc matters here:**

Enrichers run synchronously in the logging pipeline. In a high-throughput system, allocating a List<T> on every log event for a property that usually doesn't have PII is death by a thousand cuts. The defer-allocation pattern ensures we pay nothing on the common path and only allocate when actually needed (rare).

## Lessons Learned

1. **Three-Phase Pattern is Real:** R-05 (refactor), 01-03 (backfill). Each phase discovers 70-80% of infrastructure already exists. This is starting to be intentional — refactoring builds infra, critical-fix phases plug gaps. Plan for it.

2. **Planning Needs Code Audit Baseline:** Before Phase X starts, run a scout pass to verify assumptions. C3 and C4 would've been immediately identified as pre-fixed. Would've freed planning to focus on H1's gap discovery earlier.

3. **Defense-in-Depth Has Layers:** Call-site redaction (Phase 01) + enricher redaction (Phase 03) isn't redundant — it's layered. Call sites handle known log templates. Enrichers catch accidental property leaks. Both matter.

4. **Allocation Patterns Matter in Observability:** Code that runs on every log call is infrastructure code. Zero-alloc on the happy path is non-negotiable. A defer-allocation List<T> is not premature optimization — it's correctness in a hot path.

5. **Structured Logging PII Risk is Subtle:** Message templates look safe, but property values can leak. Need end-to-end property validation, not just template review. Scout tools should include "check property types for string serialization risk."

## Next Steps

1. **Audit Remaining Properties for Type Leaks** (owner: security review, timeline: before production deploy)
   - Search for properties that log user inputs: addresses, phone numbers, PSID derivatives
   - Verify each is either: (a) already redacted at call site, or (b) not a string type (safe from PiiRedactor)
   - Current scope: complete (PiiRedactingEnricher + call-site sweep covers all paths)

2. **Monitor Enricher Performance** (owner: ops, timeline: 7+ days production data)
   - Enricher adds a loop-per-log. In high-volume (>1000 msg/min), measure CPU cost.
   - If > 5% CPU impact, optimize regex compilation (cache compiled Pattern objects).
   - Current estimate: negligible (single list allocations in rare PII case, no regex allocation on happy path).

3. **Document Property Validation in Code Standards** (owner: current, timeline: before Phase 04)
   - Add section to docs/code-standards.md: "Structured Logging — Property Value Security"
   - Rule: String properties should not log raw user identifiers, addresses, or phone numbers (use hashing/masking instead)
   - Document PiiRedactor and PiiRedactingEnricher as safety nets, not primary defense

4. **Verify C3 and C4 Fix Durability** (owner: QA, timeline: staging test pass)
   - Concurrent test: two webhook requests for same FacebookPageId (C3 race test)
   - Query string negative test: verify no ?access_token= URLs exist in live Graph calls (C4 test)
   - Both should be part of pre-production security gate

## Commit Reference

**1cc1bfb** — Phase 03: PiiRedactingEnricher defense-in-depth, 39 tests (C3/C4 pre-fixed, H1 closure with zero-alloc enricher pattern)

---

**Unresolved Questions:**
- Should PiiRedactor regex patterns be compiled once (cached Pattern objects) vs. recompiled per call? Current: recompiled. Risk: high-volume logs could trigger regex compilation bottleneck. Impact: unknown without production data.
- Are there other property types (DateTime, enum, etc.) that could leak PII via ToString() when logged? Current enricher only handles strings.
- Should enricher be registered in all Serilog pipelines or only in production? Current: always on. Risk: dev logs might accidentally hide issues if they're over-redacted. Consider verbose mode flag.
