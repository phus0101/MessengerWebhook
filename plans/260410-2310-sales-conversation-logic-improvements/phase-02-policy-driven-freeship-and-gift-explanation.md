# Phase 02 — Policy-driven freeship and gift explanation

## Context Links
- Plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260410-2310-sales-conversation-logic-improvements/plan.md`
- Core files: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`, `src/MessengerWebhook/Services/Freeship/FreeshipCalculator.cs`, `src/MessengerWebhook/Services/GiftSelection/GiftSelectionService.cs`, `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`

## Overview
- Priority: P1
- Status: pending
- Goal: make freeship/khuyến mãi/quà tặng answers derive from active policy/program data, not hardcoded combo-count logic or vague default claims.

## Key Insights
- `FreeshipCalculator` currently hardcodes `count >= 2` or `COMBO_2`; this leaks business rule into transcript behavior.
- `TryBuildOfferResponseAsync` can speak from context-derived gift/shipping info but still produce vague default gift wording.
- Prompt examples still reinforce hardcoded freeship/gift statements, so service + prompt must be updated together.
- `GiftSelectionService` picking first mapped gift is acceptable only if transcript copy explains current eligible policy facts, not a generic promise.
- Scope stays narrow: reuse existing policy-bearing services/data already in repo; do not design a new promotion engine in this task.

## Requirements
### Functional
1. Bot answers freeship/promo/gift questions from current program/policy facts available in existing system services/data.
2. Bot must explain why offer applies or does not apply, using current policy semantics.
3. Bot must not promise a default gift when no active eligible gift/program exists.
4. Existing product mapping behavior, including `MN`, stays unchanged.
5. If current data model has no rich program label, bot may explain eligibility by policy facts instead of inventing a program name.

### Non-functional
- Keep policy decision in services, not duplicated in prompt/handler.
- Keep transcript copy concise and deterministic enough for integration assertions.

## Architecture
### Data flow
User asks about freeship/promo/gift -> `SalesStateHandlerBase` routes to offer response branch -> policy services evaluate selected products and active program metadata -> handler formats explanation from returned facts -> prompt examples reinforce policy-driven wording for AI-generated variants -> transcript returns only grounded program statements.

## Related Code Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/Freeship/FreeshipCalculator.cs`
- `src/MessengerWebhook/Services/GiftSelection/GiftSelectionService.cs`
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- `tests/MessengerWebhook.UnitTests/Services/Freeship/FreeshipCalculatorTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/GiftSelection/GiftSelectionServiceTests.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

### Create
- none

### Delete
- none

## Implementation Steps
1. Refactor freeship decision to consume current policy-bearing service/data inputs instead of quantity-count shortcut.
2. Define one narrow response contract from existing policy services to handler: eligibility, explanation, and optional program label/reason when available.
3. Remove handler fallback that implies a gift by default when system has no qualifying program.
4. Update prompt examples so AI copy mirrors policy-grounded explanations only.
5. Add tests for eligible, ineligible, and ambiguous-offer transcript paths.

## Todo List
- [ ] Replace hardcoded freeship shortcut with policy-backed evaluation
- [ ] Normalize handler response fields for shipping/gift explanation
- [ ] Remove vague default gift copy
- [ ] Update prompt examples
- [ ] Add unit and integration regressions

## Success Criteria
- Asking freeship returns answer grounded in current eligible program/policy.
- Asking quà tặng never yields vague default promise.
- Transcript does not mention `mua 2 sản phẩm là freeship` unless active policy actually says so.
- `MN` product flow remains untouched.

## Risk Assessment
- High: policy source may be incomplete in test seed, causing flaky transcript expectations. Mitigation: explicitly document required seed/stub data in test phase and keep handler fallback deterministic.
- Medium: handler/prompt drift reintroduces unsupported copy. Mitigation: integration tests assert exact policy-grounded wording patterns.
- Medium: over-refactor expands beyond transcript scope. Mitigation: limit change to response contract and existing services only.

## Security Considerations
- No auth change.
- No new data stored; only response-generation logic changes.

## Next Steps
- Blocks phase 03 only indirectly through shared handler file ownership.
- Phase 05 must lock transcript wording with seeded policy data.
