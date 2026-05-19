# Phase 04 — Quantity extraction and draft correctness

## Context Links
- Plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260410-2310-sales-conversation-logic-improvements/plan.md`
- Core files: `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`, `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: extract ordered quantities from transcript and persist them into draft items instead of forcing `Quantity = 1`.

## Key Insights
- Current draft creation hardcodes `DraftOrderItem.Quantity = 1`, so even correct transcript intent is lost downstream.
- Quantity must be parsed and stored before draft creation; patching only the draft service is insufficient.
- Scope is transcript behavior only; do not widen into product remapping or catalog normalization.

## Requirements
### Functional
1. Parser captures quantity per selected product from user transcript.
2. Conversation state persists those quantities until draft creation.
3. Draft order items use persisted quantities for each selected product.
4. Missing quantity continues to default safely to 1.
5. Multi-product transcript with explicit quantity 2 must generate correct draft quantities.
6. `selectedProductCodes` remains the product-selection list; quantities are stored separately in `selectedProductQuantities` as `productCode -> quantity`.

### Non-functional
- Keep quantity model simple: per product code integer quantity.
- Preserve backward compatibility for old state without quantity map.

## Architecture
### Data flow
User order message -> parser extracts product codes plus optional quantity tokens -> handler merges into `selectedProductQuantities` keyed by product code -> draft gate passes confirmed products/contact -> `DraftOrderService` reads quantity map; if no entry, uses 1 -> draft items persisted with transcript quantities.

## Related Code Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesMessageParserTests.cs`
- `tests/MessengerWebhook.UnitTests/Services/DraftOrders/DraftOrderServiceTests.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

### Create
- none

### Delete
- none

## Implementation Steps
1. Add quantity extraction rules for explicit transcript quantities near selected products.
2. Persist quantity map in conversation state using additive, backward-compatible keys (`selectedProductQuantities`).
3. Keep `selectedProductCodes` unchanged as product identity list; do not encode quantity by duplicating product codes.
4. Update draft creation to consume quantity map with default 1 fallback.
5. Ensure contact-confirmation flow still allows quantity state to survive until draft generation.
6. Add unit and integration tests for single-product default and multi-product explicit quantity cases.

## Todo List
- [x] Parse explicit quantities from transcript
- [x] Persist quantity map in state
- [x] Consume quantity map in draft generation
- [x] Preserve default quantity fallback
- [x] Lock behavior with tests

## Success Criteria
- Transcript ordering 2 units no longer creates draft quantity 1.
- Existing transcripts without quantity still draft quantity 1.
- Multi-product orders preserve per-item quantities.

## Risk Assessment
- High: parser over-matches numbers from phone/address as quantity. Mitigation: bind extraction to product mention context and cover negative tests.
- Medium: state merge overwrites prior quantities incorrectly on follow-up turns. Mitigation: define deterministic replacement/update rule per product code.
- Medium: draft item ordering assertions become brittle. Mitigation: assert product-code/quantity pairs, not incidental list order, in tests.
- Medium: price/shipping numbers (for example `30.000đ`) get misread as quantity. Mitigation: add negative tests for shipping/price utterances and address/phone-bearing messages.

## Security Considerations
- No auth change.
- Avoid treating phone numbers or addresses as quantity data.

## Delivery Notes
- Done: transcript quantity now persists into draft items instead of collapsing to `1`.
- Done: multi-product transcript case with quantity `2` now stays correct through draft creation.
- Done: default fallback remains `1` when no quantity is provided.
- Validation: covered by targeted unit + integration regression coverage.
