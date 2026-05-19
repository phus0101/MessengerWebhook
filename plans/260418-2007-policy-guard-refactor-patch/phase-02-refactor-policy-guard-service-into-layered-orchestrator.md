# Phase 02 — Refactor PolicyGuardService into layered orchestrator

## Overview
Priority: P1  
Status: completed  
Goal: replace monolithic keyword logic with normalizer + detectors + scorer while preserving keyword parity first.

## Files
### Create
- `src/MessengerWebhook/Services/Policy/IPolicyMessageNormalizer.cs`
- `src/MessengerWebhook/Services/Policy/DefaultPolicyMessageNormalizer.cs`
- `src/MessengerWebhook/Services/Policy/IPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/KeywordPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/RegexPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/FuzzyPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyRiskScorer.cs`
- `src/MessengerWebhook/Services/Policy/DefaultPolicyRiskScorer.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`

### Modify
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`

## Requirements
- Preserve current keyword coverage first.
- Normalize all matching inputs before detection.
- Keep classifier optional and disabled by default.
- Return rich decision metadata even when escalation is false.

## Implementation steps
1. Move built-in keyword map out of orchestrator into keyword detector.
2. Implement canonical normalization: lowercase, remove diacritics, collapse whitespace, strip noise separators, basic leetspeak substitution.
3. Re-implement current exact keyword logic on normalized text with parity tests.
4. Add regex detector for obfuscation patterns.
5. Add fuzzy detector only for curated phrase list and bounded edit distance.
6. Implement scorer using locked thresholds and category multipliers from `plan.md`.
7. Make `PolicyGuardService.EvaluateAsync` orchestrate all layers.
8. Make sync `Evaluate(string)` call deterministic-only path for compatibility.

## Success criteria
- Existing exact keyword behavior passes parity tests.
- New detectors can be turned on/off via options.
- Service returns score/action/signals consistently.
