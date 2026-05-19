# Phase 03 - Add regression and observability

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/tests/MessengerWebhook.UnitTests/Services/Policy/`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/AI/GeminiService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/appsettings.json`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/appsettings.Development.json`

## Overview
- Priority: P1
- Status: pending
- Goal: prove one-Gemini-per-message behavior and add enough telemetry to validate rollout without logging sensitive transcripts.

## Key insights
- Regression risk lives in branch interaction, not classifier correctness alone.
- Existing timeout logs identify each subsystem separately; missing piece is shared run/skip reason visibility.
- Need tests that assert call counts, not just final routing result.

## Requirements
### Functional
- Add tests for policy-stop path, deterministic-route path, intent-only path, policy-timeout suppression path.
- Add logs/counters for each branch decision.
- Confirm current timeout values still make sense once second call is suppressed.

### Non-functional
- Tests must avoid real Gemini calls.
- Logs must be structured and PII-light.

## Architecture
### Test matrix
| Layer | Case | Assertion |
|---|---|---|
| Unit | policy semantic attempted + timeout | intent detection not invoked |
| Unit | policy semantic attempted + no escalation | intent detection still not invoked under strict budget rule |
| Unit | policy semantic skipped | intent detection may run if route ambiguous |
| Unit | deterministic consult/buy/question route | no intent Gemini call |
| Integration-lite | full handler on ambiguous message | exactly one AI subsystem invoked |
| Integration-lite | manual-review phrase | escalation path returned; no intent Gemini |

### Observability fields
- `AiHotPathOrchestrationEnabled`
- `PolicySemanticAttempted`
- `PolicySemanticTimedOut`
- `IntentDetectionAttempted`
- `IntentDetectionSkippedReason`
- `PolicySemanticSkippedReason`
- `SalesRoutingResolutionSource` = `policy-stop|deterministic|intent-ai|fallback`

Prefer logs first. Counters optional if existing metrics path is simple.

### Data flow under test
`message -> policy branch metadata -> orchestration gate -> optional intent branch -> route source log`

## Related code files
### Must modify
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/...` targeted files
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `src/MessengerWebhook/appsettings.Development.json`

### Optional modify
- `src/MessengerWebhook/appsettings.json`
- logging helper/constants file if current messages are duplicated

## Implementation steps
1. Add fake/stub seams for policy semantic attempted/timed-out metadata and intent call counting.
2. Write tests asserting exact branch invocation counts.
3. Add structured logs with stable reason codes from Phase 01.
4. Keep dev timeout values only if needed for reproduction; do not widen timeouts as primary fix.
5. Add manual validation checklist using representative Vietnamese messages.

## Todo list
- [ ] Add call-count regression tests
- [ ] Add timeout suppression test
- [ ] Add deterministic-route skip tests
- [ ] Add structured run/skip logs
- [ ] Capture manual validation transcript set

## Success criteria
- Tests fail if same message can invoke both Gemini branches.
- Logs can explain every skip/run decision without reading code.
- No primary fix depends on increasing timeout values.

## Risk assessment
- High: tests couple too tightly to implementation details. Mitigation: assert subsystem invocation counts and route source only.
- Medium: log spam on every message. Mitigation: one summary log per message path, debug-level details for branch reasons.

## Security considerations
- Do not log raw message bodies.
- Manual validation transcripts used in docs/tests must be synthetic.

## Rollback
- Remove new summary logs if too noisy.
- Keep regression tests; they protect the hot-path contract.

## Next steps
- Phase 04 sets rollout order and operator safety checks.

## Unresolved questions
- Should observability land as logs only in v1, or also emit metrics if existing infra already captures counters cheaply?
