# Phase 04: Add transcript regression coverage for wrong-product failures

## Context links
- Overview: [plan.md](./plan.md)
- Tests: `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`
- Unit tests: `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

## Overview
- Priority: P1
- Status: completed
- Goal: Khóa các transcript lỗi thật để future changes không làm bot nhảy sai sản phẩm nữa.

## Requirements
- Có test case gần sát transcript user cung cấp.
- Có test cho hỏi freeship sau khi chọn product A nhưng history có product B.
- Có test cho order confirmation giữ đúng product active.
- Giữ assert semantic-first, không khóa wording vô ích.

## Architecture
Test matrix tối thiểu:
1. Greeting -> select mask -> ask unisex -> ask price -> ask promo -> ask freeship -> buy -> provide contact
2. Existing history contains product A, latest user switch to product B -> order must use B
3. Policy question must not mutate active product
4. Draft confirmation must not name wrong product

## Related code files
- Modify: `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`
- Modify: `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

## Implementation steps
1. Chuyển transcript thật thành integration tests.
2. Viết unit tests cho active-product lock helpers.
3. Assert state keys + draft items + response fragments.
4. Chạy targeted tests trước rồi full suites.

## Todo list
- [x] Thêm transcript bug reproduction test
- [x] Thêm active-product-lock unit tests
- [x] Thêm confirmation consistency assertions
- [x] Chạy build + unit + integration

## Success criteria
- Transcript lỗi hiện tại được tái hiện bằng test. Done.
- Sau fix, tests pass và khóa regression. Done.

## Progress note
- Regression coverage đã thêm cho wrong-product transcript; build pass, targeted unit tests pass, targeted integration tests pass.

## Risks
- Test fixture dữ liệu sản phẩm không map đúng transcript thật
- Assert wording quá cứng gây flaky

## Security considerations
- Không dùng fake shortcut để pass tests; kiểm tra persisted draft/state thật

## Next steps
- Sau implementation: code review + docs impact evaluation
