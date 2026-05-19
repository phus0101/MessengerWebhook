# Phase 01 — Greeting naturalness and consult transition

## Context Links
- Plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260410-2310-sales-conversation-logic-improvements/plan.md`
- Core files: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`, `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`

## Overview
- Priority: P1
- Status: pending
- Goal: fix first-turn greeting so both new and returning customers get a natural greeting plus a direct consult-transition question.

## Key Insights
- `BuildCustomerInstruction` currently customizes returning/VIP only; standard customer returns empty string.
- Naturalness pipeline can short-circuit on early small talk; first-turn greeting must still exit with consult question.
- Prompt examples still mix consult and hard CTA, so prompt + handler must be aligned together.

## Requirements
### Functional
1. First pure greeting from new customer must answer naturally and ask what they need.
2. First greeting from returning/VIP must stay personalized but still end with consult-transition question.
3. Product consult turns after greeting must not re-greet.

### Non-functional
- No duplicated greeting logic across prompt/handler.
- Keep messages short, transcript-like.

## Architecture
### Data flow
First user message -> conversation history count check -> customer lookup sets returning flags -> greeting instruction builder selects standard/returning/VIP first-turn rule -> AI response or fallback emits greeting + consult question -> `vipGreetingSent` or equivalent prevents repeated greeting.

## Related Code Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

### Create
- none

### Delete
- none

## Implementation Steps
1. Add explicit standard-customer first-turn instruction path in `BuildCustomerInstruction`.
2. Require consult-transition wording after greeting for both standard and returning/VIP paths.
3. Add a deterministic fallback path for pure-greeting turns so first-turn behavior does not rely entirely on AI wording.
4. Remove prompt examples that allow greeting without follow-up need question.
5. Ensure repeated turns skip greeting and continue consult normally.
6. Add unit/integration coverage for new vs returning first-turn behavior.

## Todo List
- [ ] Define first-turn rule for standard customer
- [ ] Align returning/VIP copy with same transition requirement
- [ ] Remove contradictory prompt examples
- [ ] Add regression tests

## Success Criteria
- New customer says "hi" -> reply contains greeting + asks need/product interest.
- Returning customer first turn still personalized but also asks current need.
- Second consult turn does not repeat greeting.
- Tests assert the two required semantic parts on first-turn greeting: natural welcome + consult-transition question.

## Risk Assessment
- High: greeting becomes too generic and hurts returning/VIP tone. Mitigation: preserve tier-specific wording, only normalize consult transition.
- Medium: small-talk shortcut bypasses rule. Mitigation: transcript integration test for first-turn greeting path.

## Security Considerations
- No auth change.
- No PII expansion beyond existing remembered customer context.

## Next Steps
- Blocks phases 2-5 because transcript turn-1 expectations depend on this baseline.
