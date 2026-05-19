---
title: "Sales conversation logic improvements"
description: "Lock transcript-first fixes for greeting, policy-driven freeship/gift copy, remembered-contact handling, quantity persistence, and regression tests."
status: completed
priority: P1
effort: 9h
branch: master
tags: [sales, transcript, policy, contact, draft-order, tests]
created: 2026-04-10
---

# Plan

## Decision
Update this existing plan. Scope matches exactly; creating a new plan would duplicate work across the same core files and split ownership.

## Goal
Align sales transcript behavior with business flow: natural first greeting + transition question, policy-driven freeship/gift answers, remembered-contact confirm/update path, correct draft quantity, and transcript-locked integration coverage.

## Scope decisions to lock before implementation
- Policy source of truth in this task stays narrow: reuse existing policy-bearing services/data (`FreeshipCalculator`, `GiftSelectionService`, current mappings/config already available in repo) and expose explainable eligibility facts from them. Do **not** design a new promotion engine in this task.
- If current data model does not carry a rich program name, bot may explain offer eligibility by current-policy facts instead of inventing a marketing label.
- Remembered-contact flow is split into explicit pending question states, not stacked booleans.
- Changing contact must unblock current-order draft immediately; save-for-future consent is a follow-up branch and must not block draft creation.
- Quantity model stays simple and explicit: `selectedProductCodes` continues to identify products, `selectedProductQuantities` stores `productCode -> quantity`, and draft creation falls back to `1` when quantity is absent.
- Transcript tests are semantic-first: lock required meaning, persisted draft data, and state transitions; do not over-lock harmless wording differences.

## Data flow
User message -> `SalesMessageParser` extracts product/contact/confirm/update hints incl quantity -> `GeminiService.DetectIntentAsync` + handler guards classify consult vs buy -> `SalesStateHandlerBase` chooses greeting/consult/policy/contact-confirm path -> existing policy services (`FreeshipCalculator`, `GiftSelectionService`, current mappings/config) provide explainable eligibility facts -> contact decision stored in `StateContext` via explicit pending question state -> `DraftOrderService` builds draft from persisted product+quantity+contact -> optional customer profile update only on explicit save-consent.

## Phases
| ID | Phase | File | Depends on | Status |
|---|---|---|---|---|
| 1 | [Greeting + consult transition](./phase-01-greeting-naturalness-and-consult-transition.md) | intro behavior | none | completed |
| 2 | [Policy-driven freeship + gift explanation](./phase-02-policy-driven-freeship-and-gift-explanation.md) | shipping/promo semantics | 1 | completed |
| 3 | [Remembered contact confirm + optional persistence](./phase-03-remembered-contact-confirmation-and-persistence.md) | contact branch + DB update | 1,2 | completed |
| 4 | [Quantity extraction + draft correctness](./phase-04-quantity-extraction-and-draft-correctness.md) | quantity state + draft items | 3 | completed |
| 5 | [Transcript integration + regression matrix](./phase-05-transcript-regression-tests.md) | unit/integration lock | 1,2,3,4 | completed |

## Dependency graph
- P1 unblocks all later phases because greeting/consult gating lives in `SalesStateHandlerBase.cs` and prompt copy.
- P2 must land before transcript tests; otherwise expected policy text remains unstable.
- P3 must land before P4 because draft gating depends on confirmed vs changed contact path.
- P4 must land before P5 because transcript order assertions need final quantity semantics.

## Shared file ownership
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`: phases 1-3 sequential only.
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`: phases 1-2 sequential only.
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`: phases 3-4 sequential only.
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`: phase 5 only.

## Key risks
- High: greeting fix regresses returning/VIP tone because `BuildCustomerInstruction` currently leaves standard customers empty. Mitigation: add explicit standard-customer first-message rule and assert returning flow unchanged.
- High: `ok em` still collapses into confirmation in wrong context. Mitigation: split buy-intent acknowledgement from remembered-contact confirmation; gate confirmation by preceding confirm prompt.
- High: freeship/gift wording still contradicts prompt examples. Mitigation: treat prompt + policy service as one phase; remove hardcoded/vague examples together.
- Medium: quantity state added in parser but lost before draft creation. Mitigation: persist structured quantity map in context and cover in integration draft assertions.
- Medium: contact update silently overwrites DB. Mitigation: require explicit "save for next time" consent flag before `CustomerIdentity` mutation.

## Backwards compatibility
No schema migration planned. Keep existing `selectedProductCodes`, `customerPhone`, `shippingAddress`, `contactNeedsConfirmation` keys valid; add new state keys compatibly. Existing conversations continue; if new quantity/contact-save keys absent, logic falls back to current single-order flow without crashing.

## Test matrix
- Unit: greeting instruction selection; consult vs buy gating; freeship policy matrix; gift explanation source selection; remembered-contact branch decisions; quantity extraction/state persistence.
- Integration: transcript replay for greeting, freeship, remembered contact reuse/change/save, gift explanation, 2-product draft quantity.
- Integration assertions: semantic fragments + persisted DB facts + state transitions first; exact full-sentence locking only for business-critical copy.
- E2E/manual: verify no mention of product remapping for MN flow; focus only on transcript behavior.

## Validation commands
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests`
- `dotnet test tests/MessengerWebhook.IntegrationTests`
- During development, run targeted transcript-related tests first, then rerun the full unit + integration suites before closing the task.

## Rollback
- P1-P4 rollback by reverting handler/parser/service/prompt changes in one commit; no data migration cleanup.
- If contact-save path ships and misbehaves, disable persistence branch while keeping confirm-only flow.
- After rollback verify: explicit buy + full contact still creates one draft; asking freeship still does not create draft early.

## Progress
- Done: natural first greeting now reads conversationally and always transitions into consultation.
- Done: freeship/gift replies now follow policy-driven behavior, not fixed 2-product hardcode.
- Done: remembered-contact flow now supports confirm old contact, use new contact for current draft, and explicit save-for-future consent only.
- Done: draft quantity bug fixed; transcript-confirmed quantity persists correctly into draft items.
- Done: targeted validation passed: unit 70/70, `ConversationFlowTests` 15/15.

## Scope change log
- No scope expansion. Kept focus on transcript-driven sales conversation behavior already defined in this plan.

## Risks
- Closed: greeting/consult transition regression risk.
- Closed: hardcoded freeship/gift explanation risk.
- Closed: remembered-contact auto-save risk.
- Closed: quantity lost before draft creation risk.

## Docs impact
Minor. Plan/docs synced to actual delivered scope. No new product docs added in this task.

## Success criteria
- First greeting for both new and returning customers includes natural welcome plus consult-transition question.
- Freeship/gift replies cite current policy/program semantics, not `productCodes.Count >= 2` copy or vague gift promises.
- Returning customer buy flow asks whether old contact is still valid; changed contact can create draft with new data; DB updates only after opt-in.
- Draft items reflect transcript quantity, including multi-product order in current failing transcript.
- Transcript integration tests fail on current bugs, pass after implementation, and do not assert product remapping changes for MN.
