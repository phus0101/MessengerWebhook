---
title: "Policy guard refactor patch"
description: "Minimal-risk refactor plan to turn PolicyGuardService from keyword-only escalation into a production-grade layered guard while preserving current handler contracts and keeping rollout incremental."
status: in_progress
priority: P1
effort: 6h
branch: master
tags: [policy-guard, safety, escalation, refactor, production-patch]
created: 2026-04-18
blockedBy: []
blocks: []
---

# Plan

## Goal
Refactor `PolicyGuardService` into a layered, production-grade guard with normalization, structured signals, scoring, and async evaluation, while preserving current behavior at call sites and minimizing implementation risk.

## Scope lock
- Keep `IPolicyGuardService` backward compatible during patch rollout.
- Preserve `PolicyDecision.RequiresEscalation` semantics for existing handlers.
- Do not redesign sales state machine, support-case workflow, or AI reply pipeline in this patch.
- Do not require schema changes.
- Do not force semantic/LLM classification in phase 1; keep it optional behind interface/flag.

## Why this needs a new plan
Current guard in `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs` is a single-pass keyword matcher. It is cheap but brittle: easy to bypass, hard to tune, impossible to audit beyond one keyword hit, and tightly couples escalation logic with response CTA logic. Production hardening needs structure without breaking the handler contract at `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`.

## Current baseline
- `IPolicyGuardService.Evaluate(string)` is synchronous and returns `PolicyDecision`.
- Main usage is in `SalesStateHandlerBase.cs`; if `RequiresEscalation == true`, bot creates support case and switches to `HumanHandoff`.
- `PolicyGuardService` also owns `EnsureClosingCallToAction`, which is a separate concern but should stay intact in this patch for minimal blast radius.
- Many unit tests instantiate `new PolicyGuardService(Options.Create(new SalesBotOptions()))` directly, so constructor churn must be controlled.

## Target design
### Layered guard pipeline
1. Normalize input into canonical form.
2. Collect deterministic signals from keyword/regex/fuzzy detectors.
3. Optionally enrich with classifier result behind interface.
4. Score combined evidence into action + reason.
5. Return backward-compatible `PolicyDecision` with richer metadata.

### Compatibility contract
- Existing handlers may continue calling `Evaluate(string)` during phase 1.
- New `EvaluateAsync(PolicyGuardRequest, CancellationToken)` becomes primary entry point.
- `Evaluate(string)` becomes an adapter building a minimal request and using deterministic detectors only.
- `EnsureClosingCallToAction` stays on `IPolicyGuardService` for now.

## Files in scope
### Must modify
- `src/MessengerWebhook/Services/Policy/IPolicyGuardService.cs`
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `src/MessengerWebhook/Services/Policy/PolicyDecision.cs`
- `src/MessengerWebhook/Configuration/SalesBotOptions.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesHandlerFallbacks.cs`
- `src/MessengerWebhook/Program.cs`

### Must create
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `src/MessengerWebhook/Services/Policy/PolicyGuardRequest.cs`
- `src/MessengerWebhook/Services/Policy/PolicyConversationTurn.cs`
- `src/MessengerWebhook/Services/Policy/PolicySignal.cs`
- `src/MessengerWebhook/Services/Policy/PolicyAction.cs`
- `src/MessengerWebhook/Services/Policy/PolicyScoreResult.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyMessageNormalizer.cs`
- `src/MessengerWebhook/Services/Policy/DefaultPolicyMessageNormalizer.cs`
- `src/MessengerWebhook/Services/Policy/IPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/KeywordPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/RegexPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/FuzzyPolicySignalDetector.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyRiskScorer.cs`
- `src/MessengerWebhook/Services/Policy/DefaultPolicyRiskScorer.cs`
- `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`
- `src/MessengerWebhook/Services/Policy/PolicyClassificationResult.cs`

### Must add tests
- `tests/MessengerWebhook.UnitTests/Services/Policy/PolicyGuardServiceTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/DefaultPolicyMessageNormalizerTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/KeywordPolicySignalDetectorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/RegexPolicySignalDetectorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/FuzzyPolicySignalDetectorTests.cs`
- update targeted handler tests only where call signature or richer decisions affect assertions

## Phases
Progress: 5/5 phases completed.

| ID | Phase | Files | Depends on | Status |
|---|---|---|---|---|
| 1 | [Introduce compatibility-safe policy DTOs and options](./phase-01-introduce-policy-guard-dtos-and-options.md) | policy DTO/config files | none | completed |
| 2 | [Refactor PolicyGuardService into layered orchestrator](./phase-02-refactor-policy-guard-service-into-layered-orchestrator.md) | service + detectors + scorer | 1 | completed |
| 3 | [Wire async evaluation into handlers and fallback constructors](./phase-03-wire-async-policy-guard-evaluation.md) | handlers + DI + fallback setup | 2 | completed |
| 4 | [Add regression and bypass-resistance tests](./phase-04-add-policy-guard-regression-tests.md) | unit tests + targeted handler tests | 2,3 | completed |
| 5 | [Rollout safety checks and docs impact review](./phase-05-rollout-safety-checks-and-docs-impact.md) | plan/docs only unless needed | 4 | completed |

## Step-by-step implementation plan
1. Add standalone policy DTOs/options first so later phases do not overload `SalesBotOptions` further.
2. Keep `PolicyDecision` source-compatible by only extending it with optional fields/defaults.
3. Move current keyword matching out of `PolicyGuardService` into `KeywordPolicySignalDetector` with behavior parity tests.
4. Introduce `DefaultPolicyMessageNormalizer` and make all detectors consume canonical text instead of raw `Contains` checks.
5. Add regex detector for obfuscated variants and fuzzy detector for light typo tolerance; keep both configurable via `PolicyGuardOptions`.
6. Add scorer that converts signals into `PolicyAction`, `Score`, `Confidence`, `Reason`, and `Summary`.
7. Make `PolicyGuardService` orchestrate normalizer -> detectors -> optional classifier -> scorer.
8. Implement `Evaluate(string)` as compatibility adapter; do not remove it in this patch.
9. After parity is proven, update `SalesStateHandlerBase` to call `EvaluateAsync(PolicyGuardRequest, ...)` with recent context.
10. Update `SalesHandlerFallbacks` and DI registration so tests and simplified constructors still work without manual mocking explosion.
11. Add unit tests for exact keywords, no-diacritic input, obfuscated input, fuzzy typos, negative phrases, and threshold behavior.
12. Run targeted unit tests first, then full unit suite.

## Scoring design to lock
### Deterministic signals
- exact keyword match: base weight `0.55`
- regex strong match: base weight `0.50`
- fuzzy typo match: base weight `0.20 * similarity`
- repeated mention in recent turns: `+0.10`
- open support case boost: `+0.15`
- draft-order + cancel/refund context: `+0.05`

### Category multipliers
- `PromptInjection`: `1.00`
- `RefundRequest`: `0.95`
- `CancellationRequest`: `0.90`
- `PolicyException`: `0.75`
- `ManualReview`: `0.60`
- `UnsupportedQuestion`: `0.45`

### Decision thresholds
- `< 0.35` -> `Allow`
- `0.35 - <0.60` -> `SafeReply`
- `0.60 - <0.80` -> `SoftEscalate`
- `>= 0.80` -> `HardEscalate`

### Hard overrides
- strong prompt-injection hit
- explicit refund/cancel request in order context
- explicit policy override request like forced extra promotion/free shipping

## Risks
- High: constructor/DI churn breaks large test surface because handlers instantiate `PolicyGuardService` directly. Mitigation: preserve a simple constructor overload and provide defaults for optional collaborators.
- High: richer guard over-escalates normal sales questions. Mitigation: add negative tests and start with conservative thresholds.
- Medium: async `EvaluateAsync` leaks into many handlers. Mitigation: only `SalesStateHandlerBase` changes in this patch; keep sync adapter elsewhere.
- Medium: fuzzy matching creates false positives for ordinary Vietnamese slang. Mitigation: limit fuzzy matching to short curated phrase list.
- Medium: option sprawl splits config between `SalesBotOptions` and `PolicyGuardOptions`. Mitigation: move only new guard-specific settings; keep CTA in `SalesBotOptions` until later cleanup.

## Backwards compatibility
- `Evaluate(string)` remains callable.
- `PolicyDecision.RequiresEscalation`, `Reason`, and `Summary` remain valid.
- Existing support-case escalation flow remains unchanged.
- No schema changes, no support-case API changes.

## Validation commands
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests --filter PolicyGuard`
- `dotnet test tests/MessengerWebhook.UnitTests`
- optionally targeted handler tests if `SalesStateHandlerBase` is touched

## Success criteria
- Current keyword-only behavior is preserved by parity tests before adding stronger detectors.
- Guard can detect obfuscated/no-diacritic/light-typo bypasses that current code misses.
- Handler contract stays stable: support cases still escalate through existing flow.
- New score/action metadata is available for logging and future tuning.
- Test suite proves both detection improvements and non-regression on normal sales traffic.

## Docs impact
Minor. If implementation lands, update `docs/code-standards.md` or `docs/system-architecture.md` only if policy/risk-engine structure becomes a maintained architectural concept.

## Rollback
- Revert new policy service files and restore old `PolicyGuardService` internals.
- Keep `PolicyDecision` optional fields if already merged; they are backward compatible.
- Revert handler call from `EvaluateAsync` back to `Evaluate(string)` if necessary.
