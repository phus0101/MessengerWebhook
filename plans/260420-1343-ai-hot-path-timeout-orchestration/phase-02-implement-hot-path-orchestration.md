# Phase 02 - Implement hot path orchestration

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/IPolicyGuardService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/AI/GeminiService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Program.cs`

## Overview
- Priority: P1
- Status: pending
- Goal: implement a minimal orchestration path that preserves both AI goals but ensures max one Gemini attempt per inbound message.

## Key insights
- Shared file pressure is highest in `SalesStateHandlerBase.cs`; keep helper surface small.
- Policy-first is already business-preferred, so orchestration should mostly live near handler entry and post-policy decision.
- A tiny per-message context object is cheaper than cross-service caching or prompt merging.

## Requirements
### Functional
- Add per-message orchestration state or helper to track whether Gemini budget is still available.
- Call policy evaluation first.
- Stop routing immediately on escalation/handoff decision.
- Only call intent detection when routing is still unresolved and budget remains available.
- Mark budget consumed when policy semantic classifier was attempted, especially on timeout/cancel.

### Non-functional
- Keep edits localized.
- Preserve existing service interfaces where possible; if interface changes, keep backward-compatible overloads.

## Architecture
### Recommended implementation shape
- Add a small request-scoped value object, e.g. `AiHotPathDecisionContext`, carried inside `SalesStateHandlerBase` flow only.
- Extend policy result handling with metadata already available or minimally added: whether semantic Gemini attempted, whether timeout occurred, reason code.
- Reuse existing flags/timeouts. Add at most one new switch: `EnableAiHotPathOrchestration`.

### Runtime sequence
1. In `SalesStateHandlerBase`, create hot-path context for inbound message.
2. Build `PolicyGuardRequest` and invoke `EvaluateAsync`.
3. Read policy result:
   - `SoftEscalate`/`HardEscalate` -> create support/handoff path, return.
   - `Allow`/non-stop -> continue.
4. Update hot-path context if policy semantic Gemini was attempted or timed out.
5. Run deterministic sales routing checks.
6. If deterministic route resolved, return without intent Gemini.
7. If unresolved and context budget available, call `DetectIntentAsync`.
8. Route by intent result.

### Data entering / transforming / exiting
- Input: raw message, conversation state, selected product/contact flags, recent turns.
- Transform 1: policy request -> policy decision + AI branch metadata.
- Transform 2: deterministic routing shortcut -> resolved route or `Ambiguous`.
- Transform 3: optional intent result -> sales route.
- Output: next handler action/state transition with one-or-zero Gemini attempts.

## Related code files
### Must modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `src/MessengerWebhook/Services/Policy/PolicyDecision.cs`
- `src/MessengerWebhook/Program.cs`

### Likely modify
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `src/MessengerWebhook/Configuration/GeminiOptions.cs`
- `src/MessengerWebhook/Services/AI/GeminiService.cs`

### Optional create
- `src/MessengerWebhook/StateMachine/Handlers/AiHotPathDecisionContext.cs`
- or a small helper file colocated with `SalesStateHandlerBase`

## Implementation steps
1. Add minimal orchestration flag and bind in `Program.cs` if needed.
2. Add budget metadata carrier with explicit states only; no generic pipeline framework.
3. Update `PolicyDecision` or adjacent result metadata so handler can know if semantic Gemini was attempted/timed out.
4. In `SalesStateHandlerBase`, reorder flow to policy-first stop path before intent detection.
5. Add deterministic routing shortcut method that returns `Resolved` vs `Ambiguous`; keep existing logic, just isolate decision point.
6. Gate `DetectIntentAsync` behind both ambiguity and remaining budget.
7. Ensure timeout path logs and exits without second Gemini call.

## Todo list
- [ ] Add orchestration state carrier
- [ ] Propagate policy branch metadata
- [ ] Reorder handler flow to policy-first stop path
- [ ] Gate intent detection on ambiguity + budget
- [ ] Add config binding for orchestration switch if needed

## Success criteria
- Handler path is readable in one pass.
- One message cannot call policy semantic Gemini then intent Gemini.
- Feature-off path can still emulate current independent behavior if required.
- No unrelated handlers need edits.

## Risk assessment
- High: touching handler order may break happy path. Mitigation: preserve existing branch bodies; only change guard order.
- Medium: extra metadata leaks too much policy detail into handler. Mitigation: expose only `semantic attempted`, `timed out`, `reason code`.
- Medium: new helper file adds indirection. Mitigation: create only if `SalesStateHandlerBase.cs` would otherwise exceed readable size.

## Security considerations
- No PII in orchestration context.
- Timeout suppression must not swallow escalation results already decided locally.

## Rollback
- Remove orchestration gate in handler.
- Revert metadata additions if noisy.
- Keep deterministic routing helper if it improves readability and is behavior-neutral.

## Next steps
- Phase 03 proves non-regression and adds observability to tune gates.

## Unresolved questions
- Is `PolicyDecision` the right place for branch metadata, or should handler use a separate wrapper result?
