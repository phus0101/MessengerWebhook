---
title: "Add regression tests for fenced Gemini JSON variants"
description: "Add minimal unit coverage for fenced JSON response variants in GeminiPolicyIntentClassifier."
status: pending
priority: P2
effort: 30m
branch: master
tags: [tests, policy, gemini]
created: 2026-04-19
---

# Plan

## Goal
Add 3-4 regression tests in existing classifier test file for fenced JSON variants. Do not change production code unless a test proves current parsing is wrong for a variant we decide must support.

## Files
- Modify: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/tests/MessengerWebhook.UnitTests/Services/Policy/GeminiPolicyIntentClassifierTests.cs`
- Read-only context: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`

## Scope
1. Add passing regression test for fenced JSON without language tag.
2. Add passing regression test for fenced JSON with CRLF newlines.
3. Add null-return regression test for malformed or invalid fenced JSON.
4. Optional 4th test only if cheap: prose around fenced block should return null unless current parser already supports it and behavior is intended.

## Data flow
Gemini raw text response -> `NormalizeResponseText` strips fence/body -> JSON deserialize -> classification/null.
Tests inject raw Gemini response text via mocked HTTP handler, then assert classifier output.

## Dependencies
- Existing test helpers only.
- No shared file conflict beyond single test file.

## Risks
- High: prose-wrapped fenced block may represent unsupported behavior; avoid forcing prod change unless requirement says support it.
- Low: overtesting parser internals; keep only user-visible regression cases.

## Backwards compatibility
Test-only change. No production behavior change by default.

## Verify
- Run targeted unit tests for `GeminiPolicyIntentClassifierTests`.
- Done = 3-4 new tests added, all targeted tests pass, no production file edit unless justified by failing must-support case.

## Rollback
Revert added test methods only. No cascade.

## Test matrix
- Unit: no language tag, CRLF, invalid fenced JSON, optional prose wrapper.
- Integration/E2E: none.

## Success criteria
- Existing standard fenced JSON test remains.
- New tests document chosen supported/unsupported variants explicitly.
- Diff limited to existing test file unless user later expands scope.

## Unresolved questions
- Should prose surrounding fenced block be treated as supported input or explicitly unsupported for now?
