# Phase 05 — Transcript regression tests

## Context Links
- Plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260410-2310-sales-conversation-logic-improvements/plan.md`
- Test context: `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`, `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: lock the transcript behaviors in integration tests so greeting, policy answers, contact confirmation, and quantity persistence cannot regress.

## Key Insights
- Existing seed currently mentions `KCN`, `KL`, `COMBO_2`; if transcript test needs `MN` eligibility/policy context, seed or test stub must be updated explicitly.
- User confirmed `MN` mapping works already; tests should only validate transcript behavior around it, not re-open mapping rules.
- Integration assertions should focus on observable transcript outputs and persisted draft facts.

## Requirements
### Functional
1. Add integration tests for natural greeting + consult transition for new and returning customer.
2. Add integration tests for freeship/promo/gift replies grounded in active policy data.
3. Add integration tests for remembered-contact confirm, contact change, and optional save-for-future branch.
4. Add integration tests that verify draft item quantities match transcript.
5. If `MN` appears in transcript scenario, seed/stub only what is necessary for transcript execution.
6. Prefer semantic-first assertions: required meaning, state transitions, and persisted draft facts before exact full-copy matching.

### Non-functional
- Keep tests deterministic with explicit seed/program setup.
- Avoid broad fixture changes unrelated to transcript behavior.

## Architecture
### Data flow
Test seed/stub defines products + active offers + optional remembered identity -> transcript messages posted through integration harness -> state machine replies observed turn by turn -> draft record loaded from test DB -> assertions validate reply content, contact persistence behavior, and item quantities.

## Related Code Files
### Modify
- `tests/MessengerWebhook.IntegrationTests/CustomWebApplicationFactory.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesMessageParserTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/Freeship/FreeshipCalculatorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/DraftOrders/DraftOrderServiceTests.cs`

### Create
- none

### Delete
- none

## Implementation Steps
1. Define minimal seeded programs/products required for offer transcript assertions.
2. Add/adjust integration fixtures so remembered-contact scenarios are reproducible.
3. Write transcript-first tests for greeting, policy explanation, remembered-contact branches, and draft quantity persistence.
4. Add unit coverage for parser/service edge cases surfaced by integration cases, including quantity false positives from phone/address/price text.
5. Verify tests avoid asserting unrelated `MN` mapping behavior.
6. Run targeted transcript-related tests first, then rerun full unit + integration suites before closing implementation.

## Todo List
- [x] Stabilize seed/test data for policy scenarios
- [x] Add greeting transcript cases
- [x] Add contact confirmation/save transcript cases
- [x] Add quantity persistence draft assertions
- [x] Backfill unit tests for edge cases

## Success Criteria
- Current failing transcript bugs are reproduced by tests before fix.
- Final suite asserts exact observable behavior for all requested scope items.
- No test expands scope into product mapping overhaul.

## Risk Assessment
- High: integration seeds diverge from production policy behavior. Mitigation: keep seed minimal, explicit, and aligned with active-program fields used by services.
- Medium: transcript copy assertions too brittle after harmless wording cleanup. Mitigation: assert required semantic fragments plus persisted data where exact wording is not business-critical.
- Medium: fixture updates affect unrelated tests. Mitigation: localize seed changes to dedicated transcript scenarios or isolated factory hooks.

## Security Considerations
- Test data only.
- No real customer data.

## Delivery Notes
- Done: transcript regression coverage now locks greeting, policy replies, remembered-contact branches, and quantity persistence.
- Validation: targeted test runs passed — unit `70/70`, `ConversationFlowTests` `15/15`.
- Scope held: tests stay focused on transcript-driven sales conversation behavior; no product-mapping expansion.
