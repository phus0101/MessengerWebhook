# Phase 04 - Add regression tests and rollout guardrails

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/tests/MessengerWebhook.UnitTests/Services/Policy/`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`

## Overview
- Priority: P1
- Status: pending
- Goal: prove narrow semantic scope, preserve sync behavior, and enable safe rollout/rollback.

## Key insights
- Real risk is false positive escalation on ordinary consultation phrases.
- Existing handler tests already cover policy escalation branch indirectly; only touch if behavior/constructor changed.
- Rollout safety mostly comes from config off-switch and deterministic fallback.

## Requirements
### Functional
- Add unit coverage for semantic positives, semantic negatives, timeout, malformed model output, disabled flag.
- Prove `Evaluate(string)` ignores semantic classifier.
- Prove `EvaluateAsync(...)` still returns deterministic decision when classifier null/errors.

### Non-functional
- Tests must be deterministic. Mock classifier boundary, not unrelated policy internals.
- Keep broad sales intent tests out of scope unless compile/integration requires them.

## Architecture
### Test matrix
- Unit: classifier parser/prompt guardrails, scorer thresholds, policy service fallback behavior.
- Integration-lite: `PolicyGuardService` with fake classifier result and real scorer.
- Regression: targeted `SalesStateHandlerBase` test only for existing escalation path continuity.

### Data flow under test
`request -> EvaluateAsync -> classifier success/failure/null -> scorer -> PolicyDecision`

## Related code files
### Likely create
- `tests/MessengerWebhook.UnitTests/Services/Policy/GeminiPolicyIntentClassifierTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/PolicyGuardServiceSemanticTests.cs`

### Likely modify
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Policy/...` existing policy tests as needed

## Implementation steps
1. Add positive examples: request staff support, ask real human, ask shop review special case.
2. Add negative examples: ask product advice, ask price, ask shipping, ask consultation before buying.
3. Add fallback cases: timeout, invalid JSON, low confidence, disabled feature.
4. Add sync-path test: `Evaluate(string)` output unaffected by semantic classifier registration.
5. Add rollout notes in config comments or plan handoff: start disabled in production, enable after log review.
6. Define observation metrics for rollout: escalation rate, classifier usage count, timeout rate, false-positive samples.

## Todo list
- [ ] Add classifier tests
- [ ] Add policy service semantic integration tests
- [ ] Add sync compatibility regression test
- [ ] Add rollout checklist

## Success criteria
- Tests fail if scope broadens into consultation/buy intent routing.
- Tests fail if sync path starts calling semantic classifier.
- Operator has clear disable path and validation steps.

## Risk assessment
- High: test suite uses weak fixtures and misses false positives. Mitigation: include close-neighbor negative phrases in Vietnamese.
- Medium: rollout without observability hides over-escalation. Mitigation: log classifier category/confidence and monitor rate deltas.

## Security considerations
- Test data must not include real customer PII.
- Logs should record category/confidence, not full sensitive transcript.

## Rollback
- Config off-switch first.
- Re-run policy tests after disabling to confirm deterministic-only behavior.

## Validation
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests --filter Policy`
- `dotnet test tests/MessengerWebhook.UnitTests --filter SalesStateHandlerBase`

## Next steps
- After merge, docs update only if policy guard architecture doc treats semantic classifier as maintained component.

## Unresolved questions
- Should rollout default stay `false` in appsettings until manual review of logs, or can dev config enable by default only?
