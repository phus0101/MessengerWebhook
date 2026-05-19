# Phase 03: Align replies and order confirmation with locked product

## Context links
- Overview: [plan.md](./plan.md)
- State flow: [phase-02-implement-product-lock-state-flow.md](./phase-02-implement-product-lock-state-flow.md)
- Handler: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: Mọi reply builder và draft confirmation phải dùng active product đã khóa, không tự trôi sang sản phẩm khác.

## Requirements
- Consultation reply dùng đúng active product.
- Shipping/policy reply dùng đúng active product.
- Contact memory reply và closing CTA không được đổi product label.
- Draft confirmation nên nhắc lại sản phẩm/tóm tắt đơn đủ rõ để khách phát hiện mismatch sớm.

## Architecture
Các điểm cần đồng bộ:
- `BuildProductConsultationReplyAsync`
- `BuildShippingConsultationReplyAsync`
- `BuildContactMemoryReplyAsync`
- `BuildContactCollectionReply`
- `BuildDraftConfirmation`
- `TryBuildOfferResponseAsync` nếu còn inject product copy theo intent

## Related code files
- Modify: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Maybe modify: `src/MessengerWebhook/Services/DraftOrders/DraftOrderService.cs`

## Implementation steps
1. Audit toàn bộ reply builders có resolve product riêng.
2. Đổi sang đọc active product summary thống nhất.
3. Chỉ fallback hỏi lại sản phẩm khi active product thực sự trống.
4. Sửa confirmation copy để tóm tắt đúng sản phẩm + giá/ship nếu business flow cho phép.
5. Đảm bảo response cuối tự nhiên, có dấu, không quá system-like.

## Todo list
- [x] Chuẩn hóa nguồn product cho mọi reply builder
- [x] Sửa closing/confirmation copy
- [x] Bỏ wording gây hiểu nhầm sản phẩm khác

## Success criteria
- Policy reply và order confirmation luôn nói đúng sản phẩm active. Done.
- Khách hỏi freeship về mask không bị trả lời bằng cream khác. Done.
- Chốt đơn không xác nhận sai product. Done.

## Progress note
- Consultation/policy/contact/draft confirmation đã đồng bộ sang locked active product xuyên suốt flow.

## Risks
- Confirmation copy dài quá
- Thay đổi wording làm test cũ fail hàng loạt nếu assert quá cứng

## Security considerations
- Không hiển thị thông tin giá/ship ngoài dữ liệu thực tế

## Next steps
- Phase 04 bổ sung transcript regression tests
