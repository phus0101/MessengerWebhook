---
title: "AI hot path timeout orchestration"
description: "Gate policy semantic classification and AI intent detection so one customer message does not burn two Gemini calls on the same hot path."
status: pending
priority: P1
effort: 8h
branch: master
tags: [ai, gemini, timeout, policy-guard, intent-detection, orchestration]
created: 2026-04-20
blockedBy: [260419-2000-policy-semantic-classifier, 260331-1955-ai-intent-detection-system]
blocks: []
---

# Plan

## Goal
Keep both AI goals. Run policy semantic classifier first for safety. Run sales intent Gemini only when policy did not escalate and routing is still ambiguous. Prevent same-message dual Gemini hot path and repeated timeout cascades.

## Data flow
`message -> deterministic policy detectors -> policy semantic gate -> PolicyDecision -> stop on escalation -> deterministic sales shortcuts -> intent gate -> Gemini intent detect only if still ambiguous -> state routing`

## Scope lock
- Keep policy semantic classifier for manual-review / human handoff only.
- Keep AI intent detection for sales routing only.
- Do not merge prompts, add DB work, or redesign the state machine.
- Prefer one new orchestration decision point over broad abstractions.

## Dependency graph
| ID | Phase | Depends on | Status |
|---|---|---|---|
| 1 | [Define runtime gating rules](./phase-01-define-runtime-gating-rules.md) | none | pending |
| 2 | [Implement hot path orchestration](./phase-02-implement-hot-path-orchestration.md) | 1 | pending |
| 3 | [Add regression and observability](./phase-03-add-regression-and-observability.md) | 1,2 | pending |
| 4 | [Rollout and safety checks](./phase-04-rollout-and-safety-checks.md) | 2,3 | pending |

## File ownership
- Phase 1: plan files only.
- Phase 2: runtime orchestration files only.
- Phase 3: tests + logs/config only.
- Phase 4: rollout checklist + appsettings defaults only.
Sequential only; no shared-file parallelism.

## Risks
- High: over-skip intent detection hurts routing. Mitigation: skip only on explicit stop conditions; keep ambiguous short replies AI-eligible.
- High: policy timeout still followed by intent Gemini. Mitigation: per-message AI budget marker; second Gemini branch denied after semantic invocation timeout/cancel.
- Medium: policy-first broadens escalation. Mitigation: semantic branch stays narrow and fail-open.
- Medium: weak logs block tuning. Mitigation: add reason-coded run/skip/timeout logs.

## Backwards compatibility
- `PolicyDecision` remains handler-compatible.
- `GeminiService.DetectIntentAsync` stays callable.
- Feature-off path preserves current behavior.
- No schema or API contract change.

## Test matrix
- Unit: gate matrix, budget marker, timeout suppression, deterministic shortcut eligibility.
- Integration-lite: one message => max one Gemini branch across policy + intent.
- Regression: manual review, consult-first, ready-to-buy, question-only, timeout fallback.
- Manual trace: log reason codes for run vs skip.

## Success criteria
- One inbound message triggers at most one Gemini call in the hot path.
- Manual-review safety still works.
- Ambiguous sales routing still reaches AI intent detection when needed.
- Logs show why each branch ran, skipped, or stopped after timeout.
- Repeated same-message dual-timeout pattern disappears in validation traces.

## Rollback
1. Disable orchestration flag.
2. If needed disable policy semantic classifier.
3. If needed disable AI intent detection.
4. Re-run targeted unit tests to confirm deterministic fallback.

## Validation
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests --filter Policy`
- `dotnet test tests/MessengerWebhook.UnitTests --filter SalesStateHandlerBase`

## Unresolved questions
- Budget rule after policy semantic success: always skip intent, or allow intent only when semantic path was skipped entirely?
- Reuse existing flags only, or add one dedicated orchestration flag?
