# Phase 01 - Define runtime gating rules

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/AI/GeminiService.cs`

## Overview
- Priority: P1
- Status: pending
- Goal: lock explicit allow/skip rules so policy safety stays first but hot path uses at most one Gemini call.

## Key insights
- Current duplicate cost comes from two independent Gemini entry points on same message.
- Safety and routing have different business goals, so solve with orchestration, not prompt unification.
- Timeout repetition matters more than raw latency: one timeout often triggers a second one immediately after.

## Requirements
### Functional
- Define exact conditions to run policy semantic classifier.
- Define exact conditions to stop pipeline after policy decision.
- Define exact conditions to run AI intent detection.
- Define exact conditions to suppress second Gemini attempt after semantic timeout/error/cancel.

### Non-functional
- Rules must be explainable in logs with stable reason codes.
- Rules must fit current code shape; avoid speculative framework extraction.

## Architecture
### Proposed decision order
1. Build policy request and run deterministic policy detectors.
2. Run policy semantic Gemini only if `EnableSemanticClassifier=true` and deterministic policy result is not already hard-escalate.
3. If policy result is `SoftEscalate` or `HardEscalate`, stop before sales intent routing.
4. Else evaluate deterministic sales shortcuts.
5. Run AI intent Gemini only if routing remains ambiguous and message has not already consumed Gemini budget.

### Gate matrix
| Branch | Allow when | Skip when | Output |
|---|---|---|---|
| Policy semantic classifier | semantic flag on; policy request has customer text; deterministic policy result not already escalated | semantic flag off; empty/whitespace; deterministic policy already escalated; message budget already marked exhausted | semantic classification result or deterministic-only fallback |
| Intent detection | policy path did not escalate; intent flag on; state still ambiguous after deterministic shortcuts; budget not consumed | policy escalated; routing already decided locally; budget consumed; message empty; deterministic buy/consult/question shortcut matched | intent result or local routing |

### Budget rules
- `NotUsed`: no Gemini branch has run yet.
- `ConsumedByPolicy`: policy semantic Gemini attempted, regardless of success. Intent must skip.
- `ConsumedByIntent`: intent Gemini attempted.
- `SuppressedAfterTimeout`: policy semantic timed out/cancelled; intent must skip and log suppression reason.

This is intentionally strict. Simpler and safer than allowing two Gemini attempts in one request.

### Data flow
`customer message -> policy deterministic -> optional semantic Gemini -> policy action -> stop or continue -> deterministic routing -> optional intent Gemini -> next sales action`

## Related code files
### Likely modify
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `src/MessengerWebhook/Configuration/GeminiOptions.cs`
- `src/MessengerWebhook/Services/AI/GeminiService.cs`

### Optional create
- One small orchestration helper/value object near state handler or policy service if current methods become noisy

## Implementation steps
1. Define reason codes for branch decisions: `policy_semantic_allowed`, `policy_semantic_skipped_flag`, `policy_semantic_skipped_pre_escalated`, `intent_skipped_budget_consumed`, `intent_skipped_policy_escalated`, `intent_allowed_ambiguous_route`, etc.
2. Lock deterministic sales shortcuts that bypass AI: explicit buy phrase, explicit consult phrase, pure question path, existing confirm path if already decisive.
3. Decide budget scope: per inbound message handling only, not cross-message memory.
4. Define timeout semantics: policy timeout fails open for escalation decision but still burns budget to prevent second timeout.
5. Freeze minimal config surface: one orchestration switch max, plus existing branch flags/timeouts.

## Todo list
- [ ] Approve gate matrix
- [ ] Approve budget semantics
- [ ] Approve reason code list
- [ ] Approve minimal config shape

## Success criteria
- Every inbound message can be mapped to one of four outcomes: policy-stop, deterministic-route, intent-run, or fail-open-without-second-Gemini.
- No ambiguous branch remains undocumented.
- Rule set is small enough to implement without broad refactor.

## Risk assessment
- High: missing a routing branch causes hidden regressions. Mitigation: write gate matrix before code.
- Medium: too many reason codes create noise. Mitigation: only codes needed for debugging skip/run decisions.

## Security considerations
- Do not log full customer message in reason code logs.
- Timeout/failure logs should expose branch, state, and reason only.

## Rollback
- Revert orchestration rule usage and keep both systems independently callable.
- Keep reason-code enums/constants only if harmless.

## Next steps
- Phase 02 implements the exact decision order and budget propagation.

## Unresolved questions
- Should a deterministic `SafeReply` policy action also stop intent routing, or only escalate actions?
