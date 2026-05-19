# Phase 03 — Remembered contact confirmation and optional persistence

## Context Links
- Plan: `D:/Projects/Facebook Messgener Webhook Demo/MessengerWebhook/plans/260410-2310-sales-conversation-logic-improvements/plan.md`
- Core files: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`, `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`, `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: require explicit confirmation before reusing remembered contact, allow draft with changed contact, then ask whether to save new contact for future orders.

## Key Insights
- Current draft gate already checks `hasContact && hasProduct && !needsConfirmation`, but no follow-up branch exists for saving a newly provided contact.
- `BuildContactCollectionReply` mostly confirms old contact or asks for missing info; it does not model `use old`, `use new once`, `save new for next time` as separate states.
- `CustomerIdentity` must update only after explicit opt-in; otherwise transcript behavior can silently mutate future sessions.
- To avoid boolean sprawl, contact flow should use one explicit pending-question state (for example `pendingContactQuestion = confirm_old_contact | ask_save_new_contact | none`) instead of stacking multiple ambiguous flags.

## Requirements
### Functional
1. If customer has remembered contact, bot asks whether that contact is still correct before drafting.
2. If customer gives a new contact, draft uses the new contact immediately for current order.
3. After new-contact draft path, bot asks whether to save the new contact for future use.
4. Only explicit consent updates `CustomerIdentity`.
5. If customer declines saving, future sessions continue using existing stored contact.

### Non-functional
- Keep contact decision state explicit and minimal.
- Avoid ambiguous confirmations by tying interpretation to prior prompt context.

## Architecture
### Data flow
Stored customer identity found -> handler asks confirm-old-contact question -> parser classifies reply as keep-old or provide-new-contact -> state stores explicit pending question state (for example `pendingContactQuestion = confirm_old_contact | ask_save_new_contact | none`) plus current-order contact source -> once new contact is accepted, current-order draft is allowed immediately -> follow-up step asks save-for-future question when contact changed -> explicit yes triggers `CustomerIdentity` update, no leaves DB unchanged.

## Related Code Files
### Modify
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`
- `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs`
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesMessageParserTests.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

### Create
- none

### Delete
- none

## Implementation Steps
1. Define explicit remembered-contact state transitions: ask confirm, accept old, collect new, ask save preference.
2. Implement one explicit pending-question state instead of stacking multiple booleans.
3. Update parser/handler contract so a new contact reply clears old-contact confirmation block for the current draft immediately.
4. Keep save-for-future consent as a follow-up branch after current-order draft becomes valid; it must not block current-order creation.
5. Add consent-only persistence branch for `CustomerIdentity` update.
6. Update prompt guidance so AI copy asks precise yes/no questions and does not auto-save.
7. Add transcript tests for reuse old contact, change contact without save, and change contact with save.

## Todo List
- [x] Model remembered-contact confirmation states
- [x] Allow new contact to unblock current draft
- [x] Add save-for-future consent branch
- [x] Guard `CustomerIdentity` mutation behind explicit opt-in
- [x] Add regression coverage

## Success Criteria
- Returning customer with stored contact is asked to confirm before draft creation.
- Changing contact still produces draft with new contact for current order.
- Bot asks whether to save new contact for next time.
- Database identity updates only on explicit consent.

## Risk Assessment
- High: generic confirmations like `ok em` may be misread as contact-save consent. Mitigation: scope yes/no interpretation to active pending question type.
- High: new state flags can deadlock draft creation. Mitigation: keep one clear pending-contact-question enum/flag instead of many booleans.
- Medium: save-consent timing may feel late or duplicate. Mitigation: place question immediately after new contact accepted/draft-ready branch and lock via transcript tests.

## Security Considerations
- PII handling remains existing scope, but write path becomes more explicit.
- No persistence of changed contact without consent.

## Delivery Notes
- Done: returning-customer draft flow now asks to confirm old contact first.
- Done: when customer provides new contact, current draft uses new contact immediately.
- Done: save-for-future path now requires explicit consent before persistence.
- Validation: covered by targeted transcript regression tests in `ConversationFlowTests`.
