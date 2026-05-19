---
title: "Sales bot transcript production patch"
description: "Minimal-change plan to isolate and patch sales transcript branch bugs around price, gift, contact confirmation, and ambiguous follow-up questions."
status: pending
priority: P1
effort: 4h
branch: master
tags: [sales-bot, transcript, regression, minimal-change]
created: 2026-04-14
blockedBy: []
blocks: []
---

# Plan

## Goal
Vá transcript sales bot theo hướng production-ready với thay đổi tối thiểu: xác định đúng nhánh logic gây sai giá/quà/contact confirmation/follow-up `"thông tin nào?"`, sửa root cause, thêm regression tests, và chốt docs impact.

## Scope lock
- Chỉ đụng flow hiện có trong `SalesStateHandlerBase`, `SalesMessageParser`, `ConsultingStateHandler`, `DraftOrderStateHandler`, `ProductMappingService`, `DraftOrderService`, và test files user chỉ định.
- Không redesign prompt/naturalness pipeline, không đổi schema, không thêm promotion engine mới.
- Ưu tiên patch tại branch/routing/state sync; tránh đổi copy rộng nếu không phải root cause.

## Root-cause hypotheses to verify first
1. `SalesStateHandlerBase` reuse `selectedGift*` / `shippingFee` cũ khi active product đổi hoặc khi policy reply chạy trước sync context đầy đủ.
2. `ResolveCurrentProductAsync` + `GetActiveProductOrResolveAsync` giữ product lock đúng phần lớn case, nhưng branch policy/contact có thể trả lời từ state cũ thay vì state vừa resolve.
3. `CaptureCustomerDetailsAsync` chỉ clear `contactNeedsConfirmation` cho confirm keyword rõ; câu mơ hồ như `"thông tin nào?"` có thể bị route sai thành hỏi tiếp/policy thay vì clarify missing/remembered contact state.
4. `BuildMissingInfoPrompt` / `BuildContactMemoryReplyAsync` / `BuildContactCollectionReplyAsync` có overlap trách nhiệm, dễ phát sinh wrong follow-up khi user hỏi lại sau prompt xác nhận contact.
5. `DraftOrderService` lấy gift/price/shipping từ persisted context; nếu context sync sai trước create draft thì draft sẽ hợp thức hóa transcript lỗi.

## Data flow
User message -> `SalesMessageParser` trích xuất contact + quantity + confirmation hint -> `SalesStateHandlerBase` detect intent/question type/product context -> sync `selectedProductCodes` + `selectedGift*` + `shippingFee` + contact flags -> build reply hoặc create draft qua `DraftOrderService` -> tests xác nhận transcript text fragments + persisted draft fields.

## Phases
| ID | Phase | Files | Depends on | Status |
|---|---|---|---|---|
| 1 | Reproduce and pin failing branches | read target src/tests only | none | pending |
| 2 | Apply minimal root-cause patch | `SalesStateHandlerBase.cs`, maybe `SalesMessageParser.cs`, maybe `ProductMappingService.cs` / `DraftOrderService.cs` | 1 | pending |
| 3 | Add regression coverage | listed unit + integration tests | 2 | pending |
| 4 | Verify docs impact and rollout notes | plan only unless evergreen doc truly needed | 3 | pending |

## Step-by-step implementation plan
1. Trace 4 symptom paths from transcript/tests: price reply, gift/policy reply, remembered-contact confirmation, follow-up `"thông tin nào?"`.
2. For each path, map exact guards and state keys touched: intent, `selectedProductCodes`, `selectedGiftCode`, `selectedGiftName`, `shippingFee`, `contactNeedsConfirmation`, `pendingContactQuestion`, `customerPhone`, `shippingAddress`.
3. Confirm whether bug is stale-state reuse, wrong branch precedence, or ambiguous-question handling; prefer 1 smallest fix point over scattered copy edits.
4. Patch branch ordering in `SalesStateHandlerBase` only if symptom comes from wrong route precedence.
5. Patch `SalesMessageParser` only if ambiguous phrases like `"thông tin nào?"` need explicit classification to avoid false confirm / false missing-info behavior.
6. Patch policy-context sync only where product/gift/shipping state can drift from active product before response or draft creation.
7. Keep `ConsultingStateHandler` / `DraftOrderStateHandler` untouched unless they hard-stop corrected flow.
8. Add unit tests for parser/handler branch decisions and stale-state prevention.
9. Add integration transcript tests covering end-to-end persisted draft facts: correct product, price basis, gift, shipping fee, contact confirmation path, and clarify follow-up text.
10. Run targeted suites first, then full relevant unit/integration suites.

## File ownership
- Phase 1: read-only, no ownership conflict.
- Phase 2: `SalesStateHandlerBase.cs` primary owner; secondary only if root cause proven (`SalesMessageParser.cs`, `ProductMappingService.cs`, `DraftOrderService.cs`). No parallel edits on same file.
- Phase 3: test files only.
- Phase 4: plan/docs only.

## Risks
- High: fixing branch precedence may regress legit product-switch or shipping-policy answers. Mitigation: add regression pairs for both failing and known-good paths.
- Medium: parser tweak may overfit Vietnamese phrase `"thông tin nào?"`. Mitigation: keep heuristic narrow and gated by pending contact state.
- Medium: draft assertions may pass while reply text still wrong. Mitigation: assert both transcript fragments and persisted DB fields.

## Backwards compatibility
Không đổi schema, không đổi state-key contract cũ. Nếu key mới thật sự cần, phải optional và fallback về behavior hiện tại. Existing conversations phải tiếp tục chạy nếu thiếu key mới.

## Test matrix
- Unit: parser confirm/clarify classification; handler branch precedence; policy reply uses synced product/gift/shipping state.
- Integration: transcript replay for price/gift/contact/follow-up; draft fields reflect same resolved state.
- E2E spot-check: `ConversationFlowTests`, `NaturalnessE2EScenarioTests` only for non-regression around conversational follow-up tone.

## Rollback
Revert patch commit only in touched handler/parser/service/test files. Verify after rollback: basic buy flow still creates draft, shipping/policy question still does not auto-create draft, returning-customer confirmation still asks correctly.

## Success criteria
- Reproduced failing branches mapped to exact guards/state keys.
- Minimal patch touches no more files than root cause requires.
- New regression tests fail before patch, pass after patch.
- No regression on existing locked-product, remembered-contact, and shipping-policy scenarios.
- Docs impact stated explicitly before merge.

## Docs impact
Expected none or minor. If behavior change is only bug-fix and no operator workflow changes, update plan/changelog only; no evergreen docs. If contact clarification wording or policy semantics become user-visible business rule, add minor note to `docs/system-architecture.md` or changelog in implementation phase.

## Unresolved questions
- Transcript examples cụ thể cho 4 lỗi có đang tồn tại sẵn trong failing tests/logs hay cần replay từ real conversation?
- `"thông tin nào?"` mong muốn exact reply là clarify missing contact, clarify remembered contact, hay liệt kê cả 2 tùy state?
