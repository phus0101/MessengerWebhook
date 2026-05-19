# Phase 02 - Implement policy semantic classifier

## Context links
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/Policy/PolicyGuardRequest.cs`
- `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/src/MessengerWebhook/Services/AI/IGeminiService.cs`

## Overview
- Priority: P1
- Status: pending
- Goal: add narrow semantic classifier implementation that detects requests for human/shop staff/manual review.

## Key insights
- Broad `IGeminiService.DetectIntentAsync(...)` already exists but classifies sales flow intents; reusing it would mix scopes.
- Policy classifier should use lower blast radius prompt and return only policy escalation semantics.
- `PolicyGuardRequest` already provides enough context: message, state, recent turns, support case flags, selected products.

## Requirements
### Functional
- Detect explicit and paraphrased requests for human help/manual review.
- Catch phrases like asking for staff support, person review, real human handling, shop checking special case.
- Ignore normal sales consultation, product questions, pricing, shipping, order confirmation.
- Return null on timeout/error/malformed response.

### Non-functional
- Async only.
- Time-bounded.
- Conservative: false negative better than false positive in v1.

## Architecture
### Candidate implementation
Create `GeminiPolicyIntentClassifier` under `Services/Policy`.

### Data flow
`PolicyGuardRequest + normalizedMessage -> prompt builder -> Gemini call -> parse narrow JSON -> validate category/confidence -> PolicyClassificationResult?`

### Prompt boundaries
- Binary or ternary scope only: `manual_review`, `not_manual_review`, `uncertain`.
- Must say what is not in scope: buying intent, product advice, FAQ, shipping, price, stock.
- Include Vietnamese positive and negative examples.

## Related code files
### Likely modify
- `src/MessengerWebhook/Services/Policy/IPolicyIntentClassifier.cs` if docs need tightening

### Likely create
- `src/MessengerWebhook/Services/Policy/GeminiPolicyIntentClassifier.cs`
- `src/MessengerWebhook/Services/Policy/PolicySemanticClassifierPrompt.cs` only if prompt builder would push file over 200 LOC

### Likely read only
- `src/MessengerWebhook/Services/AI/GeminiService.cs`
- `src/MessengerWebhook/Services/AI/IGeminiService.cs`

## Implementation steps
1. Decide integration shape with Gemini stack: call existing generic send method or a small dedicated classify helper; prefer smallest diff.
2. Build prompt with strict scope and JSON-only response.
3. Include only needed context fields: current message, last 2-3 turns, current state, open support case flag.
4. Parse response safely. Reject broad categories or low-confidence uncertain outputs.
5. Map positive hit to `SupportCaseReason.ManualReview` and explanation text suitable for logs.
6. Keep implementation small; extract prompt/parser helpers only if file would exceed size rule.

## Todo list
- [ ] Create classifier implementation
- [ ] Add prompt examples for positive and negative cases
- [ ] Add timeout/error handling
- [ ] Add structured parser validation

## Success criteria
- Positive phrase variants trigger semantic result.
- Common consultation phrases do not trigger result.
- Timeout/error returns null and does not break request flow.

## Risk assessment
- High: classifier drifts into broad consulting-vs-buying logic. Mitigation: binary scope prompt + negative examples.
- High: false positives escalate ordinary consultation. Mitigation: conservative threshold, explicit exclusions, regression tests.
- Medium: model output format instability. Mitigation: strict parser, null-on-invalid.

## Security considerations
- Avoid sending full long history; last few turns only.
- Keep logs free of raw customer PII when possible.

## Rollback
- Remove DI registration / class and rely on deterministic detectors.

## Validation
- `dotnet build`
- targeted unit tests from phase 04 once added

## Next steps
- Phase 03 wires classifier into scorer and DI.

## Unresolved questions
- Is existing `IGeminiService.SendMessageAsync` enough, or is a tiny policy-specific AI abstraction cleaner with same blast radius?
