---
title: "Sales prompt rewrite and product-lock conversation flow"
description: "Rewrite the sales bot prompt/rule contract and harden conversation flow so replies, policy answers, and order confirmation stay locked to the customer’s active product context."
status: completed
priority: P1
effort: 6h
branch: master
tags: [sales-bot, prompt, conversation-flow, product-lock, transcripts]
created: 2026-04-13
blockedBy: []
blocks: [improve-returning-customer-naturalness]
---

# Plan

## Goal
Viết lại prompt/rule của bot bán hàng theo hướng ngắn, enforceable, bám state machine thực tế; đồng thời thiết kế lại flow hội thoại để bot không bị nhảy sai sản phẩm khi trả lời tư vấn, freeship/khuyến mãi, và chốt đơn.

## Why this plan exists
Bug transcript hiện tại không còn nằm ở mỗi câu copy. Lỗi gốc là **product context không được khóa đủ chặt** giữa parser, history recovery, policy reply, AI prompt, và draft confirmation. Chỉ sửa prompt sẽ không đủ; chỉ sửa code routing cũng không đủ.

## Scope lock
- Giữ nguyên kiến trúc hiện tại: `SalesStateHandlerBase.cs` + `SalesMessageParser.cs` + `sales-closer-system-prompt.txt`.
- Không mở scope sang redesign toàn bộ emotion/tone/small-talk pipeline.
- Không tạo promotion engine mới.
- Không thêm memory AI dài hạn mới ngoài state keys cần thiết cho product lock.
- Tập trung transcript correctness + naturalness vừa đủ.

## Root causes to address
1. Prompt hiện tại nhiều rule nhưng chưa có **product-lock protocol** rõ ràng.
2. `BuildNaturalReplyAsync` chỉ đưa `San pham dang quan tam` ở mức mơ hồ; chưa nói bot phải giữ nguyên active product trừ khi khách đổi rõ ràng.
3. `ResolveCurrentProductAsync()` có thể lấy sản phẩm từ message hoặc history khi context rỗng; cần guard để không overwrite active product sai lúc khách chỉ hỏi policy/contact.
4. `TryExtractProductFromHistoryAsync()` có AI ambiguity resolution; đây là điểm có thể kéo sai sản phẩm nếu lịch sử có nhiều candidate.
5. `BuildShippingConsultationReplyAsync()` và `BuildDraftConfirmation()` chưa có lớp xác nhận “đơn này đang nói về sản phẩm nào” theo transcript hiện tại.
6. Tests hiện có cover nhiều case tốt, nhưng chưa khóa đủ các transcript kiểu “đang hỏi mặt nạ ngủ nhưng bot trả lời kem trị nám”.

## Decision
Tạo **plan mới** thay vì sửa plan completed cũ:
- Plan `260410-2310-sales-conversation-logic-improvements` đã completed và là baseline gần nhất.
- Plan pending `improve-returning-customer-naturalness` chỉ cover một lát cắt greeting/tone; plan mới rộng hơn và thay thế hướng cũ.

## Cross-plan dependency
- Plan mới **blocks** `improve-returning-customer-naturalness` vì sẽ thay thế hướng tiếp cận hẹp bằng flow + prompt contract đầy đủ hơn.
- Không bị block bởi plan completed cũ; chỉ reuse insight và test patterns từ đó.

## Files in scope
### Must modify
- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

### May modify if needed
- `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`
- `src/MessengerWebhook/Services/AI/GeminiService.cs`

## Target design
### Product-lock contract
Tại mọi turn, hệ thống phải phân biệt rõ 3 trạng thái:
- **No active product**: khách chưa chốt/nhắm sản phẩm nào.
- **Active product locked**: có đúng 1 sản phẩm đang là chủ đề chính.
- **Product switch requested**: khách nói rõ muốn đổi sang sản phẩm khác.

### Golden rules
1. Khi đã có `active product locked`, bot không được đổi sản phẩm chỉ vì reply builder/historical assistant text chứa tên sản phẩm khác.
2. Câu hỏi policy/contact/freeship/khuyến mãi mặc định bám active product hiện tại.
3. Chỉ cho phép switch product khi message mới có tín hiệu đổi rõ ràng.
4. History recovery chỉ dùng khi **không có active product**; không dùng để override active product đang tồn tại.
5. Draft confirmation phải nhắc lại đúng sản phẩm đã khóa trước khi/hoặc trong lúc xác nhận đơn.

## Phases
| ID | Phase | File | Depends on | Status |
|---|---|---|---|---|
| 1 | [Rewrite prompt into enforceable contract](./phase-01-rewrite-sales-prompt-into-enforceable-contract.md) | prompt | none | completed |
| 2 | [Implement product-lock state flow](./phase-02-implement-product-lock-state-flow.md) | handler + parser | 1 | completed |
| 3 | [Align reply builders and order confirmation with locked product](./phase-03-align-replies-and-order-confirmation-with-locked-product.md) | consultation/policy/order replies | 2 | completed |
| 4 | [Add transcript regression coverage for wrong-product failures](./phase-04-add-transcript-regression-coverage-for-wrong-product-failures.md) | unit + integration tests | 2,3 | completed |

## Dependency graph
- P1 defines prompt contract that code must enforce.
- P2 introduces the actual state rules for active product / switch / recovery.
- P3 consumes P2 to make every user-facing reply respect the locked product.
- P4 locks regressions from transcript level downward.

## Validation commands
- `dotnet build`
- `dotnet test tests/MessengerWebhook.UnitTests`
- `dotnet test tests/MessengerWebhook.IntegrationTests`
- Run targeted transcript tests first, then full unit/integration.

## Risks
- Closed: over-aggressive product lock may ignore legitimate product switches. Mitigation implemented via explicit switch heuristics + regression tests.
- Closed: prompt rewrite may regress naturalness if it becomes too rigid. Prompt rewrite shipped and targeted tests passed.
- Closed: history recovery fallback may still mis-pick in ambiguous chats. Guard added so fallback does not overwrite locked product.
- Closed: confirmation/order copy may remain unnatural if too template-heavy. Downstream reply/confirmation flow aligned to locked product and tests passed.

## Progress snapshot
- Phase 01 complete: sales prompt/rule contract rewritten.
- Phase 02 complete: active product lock enforced in state flow.
- Phase 03 complete: consultation/policy/contact/draft confirmation aligned to locked active product.
- Phase 04 complete: regression coverage added for wrong-product transcript.
- Validation complete: `dotnet build`, targeted unit tests, targeted integration tests all pass.

## Success criteria
- Bot giữ đúng sản phẩm xuyên suốt tư vấn → policy → chốt đơn trừ khi khách đổi rõ ràng. Done.
- Bot không trả lời freeship/khuyến mãi bằng dữ liệu của sản phẩm khác. Done.
- Bot không xác nhận đơn bằng sản phẩm khác active product. Done.
- Prompt ngắn hơn, rõ hơn, có rule product-lock riêng. Done.
- Transcript tests fail on current wrong-product cases and pass after fix. Done.

## Docs impact
Minor. Chủ yếu cập nhật plan files; không cần doc evergreen trừ khi implementation thay đổi business flow đáng kể.

## Suggested implementation order
1. Rút prompt về contract ngắn, bỏ rule trùng/lệch với code.
2. Thêm state key/product-lock protocol trong handler/parser.
3. Sửa reply builders theo active product trước, history recovery sau.
4. Viết regression transcripts bám case thật của user.
