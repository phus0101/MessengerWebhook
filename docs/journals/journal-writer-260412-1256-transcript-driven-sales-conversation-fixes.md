# Transcript-Driven Sales Conversation Fixes Stopped Saying Dumb Things

**Date**: 2026-04-12 12:55  
**Severity**: Medium  
**Component**: Sales conversation flow / transcript handling  
**Status**: Resolved

## What Happened

We did this because a real user transcript exposed four ugly problems in the sales flow: the bot opened with awkward greeting transitions, it talked about freeship like it was always true, it reused remembered contact info without a clean consent path, and it could draft the wrong quantity after the user had already made it explicit.

The fix set was narrow and practical. Greeting flow now transitions into consultation instead of sounding like a dead-end welcome script. Freeship and gift replies now come from policy-driven logic instead of hardcoded promises. Remembered contact handling now asks the customer to confirm old info or provide new info, then asks explicit consent before saving the new contact for future orders. Quantity extraction now persists the chosen quantity into `selectedProductQuantities` so the draft order stops drifting from what the customer actually asked for.

## The Brutal Truth

The painful part is none of this was theoretical. The transcript made it obvious the bot was being socially clumsy and operationally careless at the same time. Hardcoding sales-policy behavior in a conversation system is lazy, and the contact-memory flow was one bad assumption away from feeling creepy. The quantity mismatch was worse: we were close to generating an order that did not match the user's words. That is how trust gets burned.

## Technical Details

Relevant code landed in:
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`

Concrete behaviors now visible in code:
- `pendingContactQuestion = "confirm_old_contact"` and later `"ask_save_new_contact"`
- shipping/gift replies built from policy helpers instead of blanket freeship claims
- quantity persistence via `context.SetData("selectedProductQuantities", quantities)`

Validation was clean:
- targeted unit tests: 70/70 passed
- targeted integration tests: 15/15 passed

## What We Tried

1. Used the transcript as the source of truth instead of guessing from happy-path tests.
2. Tightened greeting and contact-confirmation prompts to match real conversation flow.
3. Replaced hardcoded promotional language with policy-driven freeship/gift messaging.
4. Persisted explicit quantity updates at parse time so downstream draft generation uses the right number.

## Root Cause Analysis

Root cause was simple and avoidable: we optimized for getting the bot to answer, not for making it answer faithfully. Conversation copy, fulfillment policy, contact persistence, and draft construction were treated like separate concerns when the transcript proved they are one continuous flow. We shipped behavior that sounded acceptable in isolation and broke under a real customer thread.

## Lessons Learned

- Transcript review catches failures that green tests miss.
- Never hardcode promotional promises in conversation logic.
- Reusing remembered contact without explicit save/confirm steps is a trust bug, not just a UX bug.
- Quantity must be persisted at the moment it is stated, or the order draft will invent reality.

## Next Steps

1. Owner: sales-flow maintainers, this week — add transcript-based regression coverage for greeting, contact-confirmation, policy messaging, and quantity updates.
2. Owner: product/policy owner, this week — verify freeship/gift policy sources stay authoritative as promos change.
3. Owner: QA, before next release — run another transcript sweep for order-closing paths, not just consultation.

## Unresolved Questions

- None right now; the targeted test scope passed and the transcript-driven issues in scope were addressed.
