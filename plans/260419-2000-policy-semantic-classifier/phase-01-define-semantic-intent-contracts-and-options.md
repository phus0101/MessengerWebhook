# Phase 01 - Define semantic intent contracts and options

## Context links
- Related completed plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260418-2007-policy-guard-refactor-patch/plan.md`
- Related broader plan, out of scope: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260331-1955-ai-intent-detection-system/plan.md`
- Current hook: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`

## Overview
- Priority: P2
- Status: pending
- Goal: lock narrow semantic classifier contract before implementation.

## Key insights
- `IPolicyIntentClassifier` already exists but is generic enough to drift into broad intent work.
- `PolicyGuardOptions.EnableSemanticClassifier` already exists; plan should reuse it, not invent second flag unless needed.
- `PolicyDecision` and `PolicyClassificationResult` already carry enough metadata to extend without breaking sync callers.

## Requirements
### Functional
- Narrow contract to policy escalation intent only.
- Semantic result must distinguish positive hit vs no-hit vs unusable response.
- Async-only usage path; sync path stays deterministic.

### Non-functional
- No dependency on sales-state enums or broad intent categories.
- Timeout bounded by config.
- Backward compatible signatures where possible.

## Architecture
### Data flow
`PolicyGuardRequest -> normalizedMessage -> semantic classifier contract -> PolicyClassificationResult? -> scorer`

### Contract rules
- Input: normalized message + request context.
- Transform: classifier maps text/context to `manual-review` semantics only.
- Output: `PolicyClassificationResult?` with category, confidence, reason, explanation, spans.
- Null means no semantic contribution, not failure.

## Related code files
### Likely modify
- `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`
- `src/MessengerWebhook/Services/Policy/PolicyClassificationResult.cs`
- `src/MessengerWebhook/Configuration/PolicyGuardOptions.cs`

### Likely create
- `src/MessengerWebhook/Services/Policy/PolicySemanticIntent.cs` if enum needed
- `src/MessengerWebhook/Services/Policy/PolicySemanticClassifierResult.cs` only if current result record proves too generic

### Do not touch in this phase
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/AI/IGeminiService.cs`

## Implementation steps
1. Review current contract and decide if existing `PolicyClassificationResult` can express `manual-review-hit`, `none`, `unknown` without new type.
2. Prefer reuse over new DTOs. Add enum only if string category is too loose for tests and scorer logic.
3. Tighten XML docs/comments to state classifier scope: manual-review/human-support only.
4. Extend `PolicyGuardOptions` only for truly needed knobs: timeout, min confidence, maybe enable flag reuse.
5. Define failure semantics: timeout/error/malformed output => null result, deterministic path continues.

## Todo list
- [ ] Lock classifier scope comment/docs
- [ ] Lock return semantics for no-hit vs failure
- [ ] Lock config knobs and defaults
- [ ] Confirm no broad AI intent dependency added

## Success criteria
- Junior can implement classifier without guessing scope.
- Contract makes sync vs async behavior explicit.
- Config surface stays minimal and policy-specific.

## Risk assessment
- High: over-generic contract invites broad sales routing reuse. Mitigation: explicit scope comments and tests.
- Medium: too many config knobs create tuning churn. Mitigation: keep to enable + timeout + confidence.

## Security considerations
- Do not leak PII beyond data already in `PolicyGuardRequest`.
- Avoid sending unnecessary fields to model.

## Rollback
- Revert docs/comments/options additions only. No runtime impact if phase shipped alone.

## Validation
- `dotnet build`

## Next steps
- Phase 02 implements classifier behind this contract.

## Unresolved questions
- Is string `Category` enough, or should v1 use enum for deterministic scorer branching?
