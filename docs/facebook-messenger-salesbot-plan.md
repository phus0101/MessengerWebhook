# Facebook Messenger Salesbot Plan

## Summary
- Refactor chatbot from skincare-consultation focus to sales-conversion focus.
- Keep webhook, queue, Messenger API, PostgreSQL, and Gemini foundations already in the repo.
- Add sales domain entities, tenant/page foundation, local draft-order workflow, risk/VIP intelligence, human handoff scaffolding, and knowledge import plumbing.
- Prefer inbox-first MVP while preparing data isolation and page routing for multi-branch rollout.

## Implementation Scope
- `Phase 0`: harden configuration, remove embedded secrets, verify Nobita contract assumptions.
- `Phase 1`: add sales domain entities and knowledge import foundation.
- `Phase 2`: connect quick replies and direct product-intent messages into a unified sales entry flow.
- `Phase 3`: introduce sales-first conversation states and policy guardrails that always steer toward phone number + address capture.
- `Phase 4`: create local draft orders first, then leave Nobita submission as a dedicated integration point.
- `Phase 5+`: keep hooks ready for human handoff, multi-page routing, livestream automation, and operations tooling.

## Sales Flow
1. Customer clicks ad quick reply or sends a direct product-intent message.
2. Bot replies with product + gift + shipping policy and asks for phone number + address.
3. If customer asks follow-up questions, bot answers naturally but always closes with the same order CTA.
4. If customer provides phone number + address, system creates a local draft order for manual review.
5. If conversation hits policy boundaries or unsupported scenarios, system creates a support case and locks the bot for that thread.

## New Building Blocks
- `INobitaClient`
- `IKnowledgeImportService`
- `IPolicyGuardService`
- `ICaseEscalationService`
- `IBotLockService`
- `ILiveCommentAutomationService`
- Tenant/page entities, customer identity, VIP/risk signals, draft order, support case, bot lock, and knowledge snapshot storage.

## Guardrails
- Never promise free shipping, gifts, discounts, refunds, or cancellations outside configured policy.
- Risky customers are flagged for staff review, not rejected by the bot.
- VIP detection only changes tone; pricing and promotions remain policy-driven.
- Human-handoff cases pause bot replies until the case is completed and the customer sends the next message.

## Rollout Notes
- Start with one Facebook page and inbox-first flow.
- Measure contact capture rate, draft-order rate, response latency, handoff rate, VIP/risk hit rate, and Nobita failure rate.
- Expand to additional pages only after pilot metrics are stable.
