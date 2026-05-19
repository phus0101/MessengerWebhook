---
title: "Policy semantic classifier for manual-review guard"
description: "Add narrow semantic human-support/manual-review classification inside policy guard without expanding sales intent routing."
status: pending
priority: P2
effort: 7h
branch: master
tags: [policy-guard, semantic-classifier, manual-review, human-support, rollback-safe]
created: 2026-04-19
blockedBy: []
blocks: [260420-1343-ai-hot-path-timeout-orchestration]
---

# Plan

## Goal
Catch narrow manual-review / human-support requests in `PolicyGuardService` via semantic classification, eg "tôi muốn nhân viên bên sốp hỗ trợ trường hợp này", without touching broad sales intent routing.

## Scope lock
- Only classify `manual-review` / `human-support` / `need-human` intent inside policy guard.
- Do not redesign `SalesStateHandlerBase` routing.
- Do not replace or extend broad AI intent system in `Services/AI`.
- Keep `IPolicyGuardService.Evaluate(string)` sync-compatible.
- Semantic path runs only from `EvaluateAsync(PolicyGuardRequest, ...)`.
- No schema/db migration.

## Dependency decision
- `260418-2007-policy-guard-refactor-patch`: not blocked. It is already completed and provides the async policy pipeline this plan needs.
- `260331-1955-ai-intent-detection-system`: not blocked. Different scope: broad sales journey routing vs narrow policy escalation intent.

## Data flow
`message + request context -> normalizer -> deterministic detectors -> optional semantic manual-review classifier (async only) -> scorer -> PolicyDecision -> existing handoff flow`

## Phases
| ID | Phase | Depends on | Status |
|---|---|---|---|
| 1 | [Define semantic intent contracts and options](./phase-01-define-semantic-intent-contracts-and-options.md) | none | pending |
| 2 | [Implement policy semantic classifier](./phase-02-implement-policy-semantic-classifier.md) | 1 | pending |
| 3 | [Integrate classifier into policy scoring](./phase-03-integrate-classifier-into-policy-scoring.md) | 1,2 | pending |
| 4 | [Add regression tests and rollout guardrails](./phase-04-add-regression-tests-and-rollout-guardrails.md) | 2,3 | pending |

## File ownership
- Phase 1: policy contracts/options only
- Phase 2: semantic classifier files only
- Phase 3: orchestrator/scoring/DI only
- Phase 4: tests + config validation only
No parallel phase should edit same file.

## Success criteria
- Async policy evaluation escalates explicit human-support/manual-review requests missed by keyword detectors.
- Sync `Evaluate(string)` behavior unchanged except existing deterministic logic.
- No new dependency from policy guard to broad sales intent routing.
- Feature can be disabled by config and code falls back to deterministic detectors only.
- Unit tests cover positive, negative, timeout, malformed-response, and fallback cases.

## Validation commands
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests --filter Policy`
- `dotnet test tests/MessengerWebhook.UnitTests --filter SalesStateHandlerBase`

## Rollback
1. Set `PolicyGuard:EnableSemanticClassifier=false`.
2. If needed, revert semantic classifier registration and scorer weighting.
3. Keep interface/DTO additions if already merged when backward compatible.
4. Verify `EvaluateAsync` still works with deterministic detectors only.

## Unresolved questions
- Should semantic classifier read `RecentTurns` only, or also explicit commerce snapshot fields once available?
- Should low-confidence semantic hits map to `SafeReply` or be ignored entirely in v1?
