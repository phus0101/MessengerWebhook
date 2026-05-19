# Phase 04 — Add policy guard regression tests

## Overview
Priority: P1  
Status: completed  
Goal: lock both parity and improved bypass resistance before rollout.

## Files
### Create
- `tests/MessengerWebhook.UnitTests/Services/Policy/PolicyGuardServiceTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/DefaultPolicyMessageNormalizerTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/KeywordPolicySignalDetectorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/RegexPolicySignalDetectorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/FuzzyPolicySignalDetectorTests.cs`

### Modify if needed
- targeted handler tests around escalation branch only

## Test matrix
- exact keyword hit
- no-diacritic hit
- obfuscated hit with separators
- light typo hit
- negative phrase that must not escalate
- threshold boundary behavior
- compatibility path via `Evaluate(string)`
- async path via `EvaluateAsync(PolicyGuardRequest, ...)`

## Implementation steps
1. Add parity tests for current built-in keywords.
2. Add normalization tests for Vietnamese diacritics and spacing noise.
3. Add regex tests for obfuscated variants.
4. Add fuzzy tests with strict bounds to avoid overreach.
5. Add scoring threshold tests.
6. Add one handler-level regression test proving support-case escalation still happens through the same path.

## Success criteria
- New tests fail on current brittle implementation where expected.
- Refactored guard passes unit tests and targeted handler regression.
