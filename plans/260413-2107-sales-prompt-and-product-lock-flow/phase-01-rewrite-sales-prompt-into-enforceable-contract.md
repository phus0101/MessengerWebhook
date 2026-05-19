# Phase 01: Rewrite sales prompt into enforceable contract

## Context links
- Overview: [plan.md](./plan.md)
- Prompt file: `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- Main caller: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: Rút prompt hiện tại thành contract ngắn, rõ, enforceable bởi code hiện có.

## Requirements
- Giữ natural tone nhưng bỏ rule trùng, example thừa, self-checklist dài dòng.
- Thêm section product-lock rõ ràng.
- Phân tách 3 nhóm: greeting, consultation/policy, order closing.
- Prompt không được tự cho phép đổi sản phẩm khi state chưa đổi.

## Architecture
Prompt mới nên nhận input theo state thực tế:
- customer message
- active product summary
- contact summary
- customer tier/tone hints
- CTA instruction từ handler

Prompt chỉ nên quyết định wording; state machine quyết định flow.

## Related code files
- Modify: `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- Read: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

## Implementation steps
1. Audit prompt hiện tại, gạch bỏ các rule trùng với code guard.
2. Viết lại theo format ngắn: role → hard constraints → product-lock rules → response style rules.
3. Thêm rule: nếu active product đã có thì mọi trả lời policy/chốt đơn phải bám đúng sản phẩm đó.
4. Thêm rule: chỉ đổi sản phẩm khi khách nói rõ đổi/chọn lại sản phẩm khác.
5. Giảm example count; chỉ giữ vài positive examples thật cần thiết.

## Todo list
- [x] Xóa instruction thừa/trùng
- [x] Thêm product-lock rules
- [x] Chuẩn hóa greeting/consult/order sections
- [x] Giảm prompt length nhưng giữ đủ business constraints

## Success criteria
- Prompt ngắn hơn rõ rệt. Done.
- Có rule explicit chống nhảy sản phẩm. Done.
- Không còn mâu thuẫn giữa prompt và state machine. Done.

## Progress note
- Prompt/rule bot bán hàng đã được rewrite theo contract ngắn, enforceable, bám state machine thực tế.

## Risks
- Prompt quá ngắn làm giảm nuance
- Prompt quá dài giữ lại vấn đề cũ

## Security considerations
- Không bịa chính sách
- Không cho phép model tự ý cấp ưu đãi ngoài policy

## Next steps
- Phase 02 triển khai state flow để enforce contract này
