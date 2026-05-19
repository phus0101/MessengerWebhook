# Phase 04 - Rollout and safety checks

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/appsettings.json`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/appsettings.Development.json`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/docs/project-changelog.md`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/docs/system-architecture.md`

## Overview
- Priority: P2
- Status: pending
- Goal: ship orchestration safely, with measurable validation and fast rollback if routing or escalation quality regresses.

## Key insights
- This change is orchestration, not model-quality tuning.
- Best rollback is config-first, not code revert.
- Success should be measured on duplicate-timeout disappearance and no obvious drop in routing quality.

## Requirements
### Functional
- Define config defaults for development and production-safe rollout.
- Define validation checklist before enabling broadly.
- Define rollback sequence for each feature flag combination.

### Non-functional
- Keep rollout steps short and operator-friendly.
- Do not require schema migration or data backfill.

## Architecture
### Rollout order
1. Merge with orchestration flag off by default if uncertainty remains.
2. Enable in development and replay representative transcripts.
3. Enable in low-risk environment with log review.
4. Review route-source distribution and timeout logs.
5. Enable as default once duplicate-timeout issue is verified resolved.

### Metrics / evidence to review
- Count of `policy semantic timeout` logs.
- Count of `intent detection timeout` logs.
- Count of messages where both branches attempted on same request should be zero.
- Handoff rate delta vs baseline.
- Deterministic vs intent-AI route distribution.

### Rollback matrix
| Scenario | Action | Expected fallback |
|---|---|---|
| Orchestration causes missed sales routing | disable orchestration flag | revert to current independent path |
| Policy semantic over-escalates | disable semantic classifier | deterministic policy + existing routing |
| Intent AI remains noisy | disable AI intent detection | deterministic routing only |
| Logs too noisy | lower log level / remove summary logs | behavior unchanged |

## Related code files
### Likely modify
- `src/MessengerWebhook/appsettings.json`
- `src/MessengerWebhook/appsettings.Development.json`
- `docs/project-changelog.md` only if implementation lands
- `docs/system-architecture.md` only if maintained architecture docs need the new gate

## Implementation steps
1. Decide default for `EnableAiHotPathOrchestration` per environment.
2. Define pre-enable checklist: build pass, targeted tests pass, manual transcript review pass.
3. Define post-enable checks: no same-message dual Gemini logs, no spike in unresolved routing complaints, no spike in handoff false positives.
4. Document rollback order in config comments or deployment notes.
5. Record docs impact as none/minor after implementation review.

## Todo list
- [ ] Lock default flag values by environment
- [ ] Lock validation checklist
- [ ] Lock rollback order
- [ ] Decide docs impact

## Success criteria
- Operator can enable/disable orchestration without code changes.
- Validation uses observable evidence, not intuition.
- Rollback avoids cascading damage to policy or routing systems.

## Risk assessment
- Medium: flag combinations become confusing. Mitigation: document supported combinations only.
- Medium: baseline unknown, so improvements are hard to prove. Mitigation: compare against current timeout logs before enable.

## Security considerations
- Rollout review should inspect aggregated logs, not raw customer transcripts.
- No new sensitive config values introduced.

## Rollback
1. Turn off orchestration.
2. If issue persists, turn off semantic classifier.
3. If still needed, turn off intent detection.
4. Re-run targeted tests and sanity transcripts.

## Next steps
- After rollout validation, close linked plans if orchestration is now the required runtime contract.

## Unresolved questions
- Should production default ship off first, or is dev-only evidence enough to default on immediately?
