# Phase 02: Implement product-lock state flow

## Context links
- Overview: [plan.md](./plan.md)
- Prompt phase: [phase-01-rewrite-sales-prompt-into-enforceable-contract.md](./phase-01-rewrite-sales-prompt-into-enforceable-contract.md)
- Main logic: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Parser: `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: Khóa sản phẩm active trong state, chỉ cho đổi khi có explicit switch signal.

## Requirements
- Không overwrite `selectedProductCodes` khi active product còn hợp lệ.
- Chỉ recover từ history khi active product trống.
- Phân biệt `product mentioned`, `active product`, `product switch intent`.
- Policy/contact questions phải reuse active product hiện tại.

## Architecture
Đề xuất state protocol:
- `selectedProductCodes`: active product hiện tại
- `lastResolvedProductSource`: direct-message | history-recovery | explicit-switch
- `productSwitchPending`: optional when message mơ hồ nhiều sản phẩm

Ưu tiên resolve:
1. explicit switch trong message mới
2. active product hiện tại
3. direct product match trong message mới nếu chưa có active product
4. history recovery nếu vẫn chưa có

## Related code files
- Modify: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Modify: `src/MessengerWebhook/StateMachine/Handlers/SalesMessageParser.cs`

## Implementation steps
1. Tạo helper xác định explicit product switch.
2. Refactor `ResolveCurrentProductAsync()` để không override active product đang tồn tại trừ khi switch rõ.
3. Refactor `TryExtractProductFromHistoryAsync()` chỉ chạy khi active product rỗng.
4. Ghi thêm source metadata khi resolve product.
5. Siết route freeship/policy/order để chỉ đọc active product đã khóa.

## Todo list
- [x] Thêm explicit switch detection
- [x] Guard history recovery
- [x] Guard direct message mapping against accidental overwrite
- [x] Lưu source metadata cho resolved product

## Success criteria
- Không còn jump product do history ambiguity. Done.
- Không đổi sản phẩm chỉ vì câu hỏi policy/contact. Done.
- Product switch hợp lệ vẫn hoạt động. Done.

## Progress note
- Active product đã được khóa trong state flow; history recovery/direct mapping không còn overwrite sai context đang lock.

## Risks
- Khóa quá chặt làm miss product switch thật
- Nhiều helper mới làm flow khó đọc nếu không giữ đơn giản

## Security considerations
- Không dùng AI ambiguity resolution để override active product đã khóa

## Next steps
- Phase 03 sửa các reply builders theo active product mới
