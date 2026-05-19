# Phase 03 - Integrate classifier into policy scoring

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/DefaultPolicyRiskScorer.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Program.cs`

## Overview
- Priority: P1
- Status: pending
- Goal: let async policy path use semantic signal safely, without changing sync compatibility or broad routing.

## Key insights
- `PolicyGuardService.EvaluateAsync(...)` already has optional classifier hook.
- `Evaluate(string)` already bypasses classifier; must stay that way.
- Main consumer already calls async path from `SalesStateHandlerBase`; likely no caller change needed.

## Requirements
### Functional
- Semantic classification only contributes in async path when feature flag enabled and classifier registered.
- Scorer maps semantic positive hit to escalation only when confidence passes threshold.
- No semantic dependency in sync `Evaluate(string)`.
- Existing handoff output stays same shape: `PolicyDecision.RequiresEscalation`, `Reason`, `Summary`.

### Non-functional
- Fail-open to deterministic path on classifier issues.
- Config-driven rollout.
- Minimal DI churn.

## Architecture
### Data flow
`EvaluateAsync -> detectors -> semantic classifier? -> scorer -> PolicyDecision`

### Integration rules
- If classifier disabled or null: same as current deterministic pipeline.
- If classifier returns null: ignore semantic signal.
- If classifier returns low-confidence result: either ignore or convert to non-escalating metadata only; decide once in phase 01.

## Related code files
### Likely modify
- `src/MessengerWebhook/Services/Policy/PolicyGuardService.cs`
- `src/MessengerWebhook/Services/Policy/DefaultPolicyRiskScorer.cs`
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`
- `src/MessengerWebhook/Program.cs`

### Likely create
- none if current scorer can absorb change

### Must not modify for scope
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs` except maybe comments/tests if needed
- `src/MessengerWebhook/Services/AI/IGeminiService.cs` broad intent contracts

## Implementation steps
1. Register semantic classifier in DI as optional policy dependency.
2. Add minimal options for confidence threshold if not already locked.
3. Update scorer to treat semantic `manual_review` hit as additive or hard override. Prefer additive first unless false negatives remain too high.
4. Make summary/reason explain semantic basis briefly for support-case audit trail.
5. Verify `Evaluate(string)` code path still uses deterministic detectors only.
6. Keep `SalesStateHandlerBase` untouched unless compile fix required.

## Todo list
- [ ] Wire DI registration
- [ ] Wire config binding
- [ ] Add scorer semantics for classifier result
- [ ] Verify sync path isolation

## Success criteria
- Async callers get semantic escalation when enabled.
- Sync callers unchanged.
- Disabling feature restores pre-phase behavior without code revert.

## Risk assessment
- High: semantic result overweighting causes escalation spikes. Mitigation: additive scoring and conservative threshold first.
- Medium: DI change breaks tests with manual constructors. Mitigation: keep classifier optional and preserve current constructor path.
- Medium: scorer coupling to free-form category strings. Mitigation: narrow accepted category set.

## Security considerations
- Summary text should be safe for logs/support records.
- Do not expose raw model text directly to user.

## Rollback
- Disable feature flag or remove DI registration.
- Revert scorer semantic branch if over-escalation seen.

## Validation
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests --filter PolicyGuard`

## Next steps
- Phase 04 proves non-regression and rollback safety.

## Unresolved questions
- Should semantic hit be hard override only for direct human-request phrases, or always additive in v1?
