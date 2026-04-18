# Sales Bot Acceptance Criteria and Test Cases

## Overview

Tài liệu này chuyển bộ quy tắc kỹ thuật của sales bot sang dạng acceptance criteria và test cases để đội dev/test dùng trực tiếp khi implement, review, và regression test.

## Scope

Áp dụng cho các luồng:

- chào hỏi và khám phá nhu cầu
- làm rõ nhu cầu mơ hồ
- tư vấn sản phẩm
- trả lời giá / khuyến mãi / ship / freeship / tồn kho
- xử lý khách cũ
- xác nhận giỏ hàng
- thu thông tin nhận hàng
- xác nhận đơn cuối cùng
- tạo draft order / chốt đơn

## Acceptance Criteria

### AC-01: Greeting must not pretend verified familiarity
**Mô tả**
Bot không được nói như đã biết khách cũ nếu chưa xác minh hồ sơ khách hàng.

**Acceptance criteria**
- Khi người dùng chỉ chào hỏi, bot phải chuyển sang hỏi nhu cầu.
- Bot không được dùng câu gợi ý đã biết khách cũ nếu `customer_verified != true`.
- Bot phải giữ giọng tự nhiên, ngắn gọn, không lan man.

**Pass examples**
- "Dạ em chào chị ạ, chị đang muốn tìm sản phẩm nào để em hỗ trợ kỹ hơn nhé?"

**Fail examples**
- "Lâu rồi mới thấy chị quay lại bên em."

### AC-02: Need clarification must be short and focused
**Mô tả**
Khi nhu cầu còn mơ hồ, bot phải hỏi 1 câu chính để làm rõ.

**Acceptance criteria**
- Nếu bot chưa đủ dữ kiện để tư vấn đúng, bot phải hỏi lại.
- Mỗi lượt chỉ nên có 1 ý hỏi chính.
- Bot không được hỏi dồn nhiều thuộc tính cùng lúc nếu chưa cần.

### AC-03: Recommendations must map to customer need
**Mô tả**
Mỗi gợi ý sản phẩm phải gắn với nhu cầu vừa được khách nhắc tới.

**Acceptance criteria**
- Bot chỉ gợi ý từ 1 đến 3 sản phẩm mỗi lượt.
- Mỗi sản phẩm gợi ý phải có lý do ngắn gọn vì sao phù hợp.
- Bot không được drift sang sản phẩm khác không liên quan nhu cầu hiện tại.

### AC-04: Business facts must be grounded before assertion
**Mô tả**
Bot chỉ được khẳng định chắc chắn các fact kinh doanh khi đã có dữ liệu xác minh.

**Acceptance criteria**
- Giá sản phẩm đang active có thể được trả lời trực tiếp khi runtime đã resolve được sản phẩm.
- Khuyến mãi chỉ được khẳng định chắc khi `promotion_confirmed == true`.
- Chính sách ship / freeship chỉ được khẳng định chắc khi `shipping_policy_confirmed == true`; nếu chưa thì phải dùng phrasing kiểu "theo dữ liệu em đang thấy" hoặc "em kiểm tra lại".
- Tồn kho chỉ được khẳng định chắc khi `inventory_confirmed == true`.
- Runtime hiện giữ `promotion_confirmed`, `shipping_policy_confirmed`, `inventory_confirmed` theo hướng conservative trong focused suite price/shipping.
- Bot không được trả lời business fact theo kiểu chắc chắn khi flag xác minh chưa đủ.

### AC-05: Product reference ambiguity must be clarified
**Mô tả**
Khi người dùng nói mơ hồ về sản phẩm, bot phải hỏi lại trước khi chốt.

**Acceptance criteria**
- Nếu người dùng dùng các cụm như "2 sản phẩm đó", "cái kia", "món đó", "ship chỗ cũ" và history gần đây còn hơn 1 khả năng hợp lý, bot phải clarify.
- Runtime không được auto-pick chỉ vì một món được nhắc gần hơn trong history.
- Bot phải đưa ra câu xác nhận ngắn, rõ các lựa chọn vừa được nhắc gần đây.

### AC-06: Active product must remain stable during policy Q&A
**Mô tả**
Khi khách hỏi tiếp về giá / ship / khuyến mãi, bot phải giữ đúng sản phẩm đang được quan tâm.

**Acceptance criteria**
- Sau khi `active_product` đã được thiết lập, bot phải tiếp tục trả lời theo sản phẩm đó cho đến khi khách đổi ý rõ ràng.
- Hỏi về ship / khuyến mãi / combo không được làm bot drift sang sản phẩm khác.
- Nếu product context tạm thiếu, hệ thống phải ưu tiên phục hồi từ history gần nhất trước khi hỏi AI disambiguation.

### AC-07: Returning customer flow must verify before reuse
**Mô tả**
Với khách cũ, bot phải xác minh trước khi dùng thông tin cũ.

**Acceptance criteria**
- Khi khách nói đã từng mua rồi mà `customer_verified == false`, bot phải xin số điện thoại để tra cứu trước.
- Nếu tìm thấy hồ sơ cũ, bot phải đọc lại thông tin ngắn gọn và yêu cầu khách xác nhận.
- Bot không được tự dùng địa chỉ cũ hoặc số điện thoại cũ nếu khách chưa đồng ý.
- Nếu chỉ có 1 phần thông tin cũ, bot phải hỏi phần còn thiếu thay vì fail im lặng.

### AC-08: Cart confirmation must happen before order creation
**Mô tả**
Bot phải xác nhận rõ khách đang mua sản phẩm nào trước khi thu thông tin hoặc tạo đơn.

**Acceptance criteria**
- Nếu giỏ hàng chưa rõ, bot phải hỏi xác nhận cart.
- `cart_items` chỉ được chứa các sản phẩm khách đã xác nhận.
- Khi khách sửa món trong lúc đang checkout, bot phải rebuild summary và xin xác nhận lại.
- Bot không được merge cart im lặng theo suy đoán.

### AC-09: Final confirmation must include full order summary
**Mô tả**
Trước khi tạo draft/chốt đơn, bot phải tóm tắt đầy đủ.

**Acceptance criteria**
- Summary cuối phải gồm: sản phẩm, số lượng, tiền hàng, ship/phí ship, tổng tiền tạm tính, số điện thoại, địa chỉ, và hướng dẫn xác nhận bước tiếp theo.
- Bot chỉ bắt đầu render final summary khi đã có cart, số điện thoại, và địa chỉ.
- Sau khi summary đã render, `final_price_summary_ready == true` phải được giữ như gate trước khi tạo draft.
- Nếu khách hỏi chen ngang về summary, bot phải resend/tóm tắt lại thay vì tạo draft ngay.
- Bot không được bỏ qua bước xác nhận cuối.

### AC-10: Draft order or order confirmation requires explicit customer confirmation
**Mô tả**
Bot chỉ được tạo draft order hoặc xác nhận đơn khi khách đã confirm summary cuối.

**Acceptance criteria**
- Chỉ tạo draft / confirm order khi người dùng đã xác nhận rõ ràng summary cuối.
- Không được tạo draft order khi remembered-contact confirmation vẫn đang pending.
- Sau khi tạo draft, bot phải thông báo trạng thái rõ ràng, không dùng câu máy móc hoặc sai trạng thái.

### AC-11: Bot tone must stay natural and operationally correct
**Mô tả**
Bot phải tự nhiên như nhân viên thật nhưng không quá màu mè.

**Acceptance criteria**
- Câu trả lời phải ngắn gọn, thân thiện, chuyên nghiệp.
- Không lặp "dạ", "chị ạ", "mình ạ" quá mức.
- Không dùng câu sai trạng thái như "chị nhận được rồi ạ" khi đơn chưa giao.
- Không kết thúc bằng mã kỹ thuật thô mà không giải thích trạng thái đơn.

## Test Cases

| ID | Scenario | Preconditions | Steps | Expected Result |
|---|---|---|---|---|
| TC-01 | Greeting from unknown customer | `customer_verified=false` | User: "hi sốp" | Bot chào tự nhiên và hỏi nhu cầu; không nói như đã biết khách cũ |
| TC-02 | Vague need clarification | No clear need extracted | User: "tìm sản phẩm dưỡng da" | Bot hỏi 1 câu làm rõ nhu cầu; không hỏi dồn nhiều ý |
| TC-03 | Recommendation with reasoning | Need extracted = outdoor skin stress | User describes pain point | Bot gợi ý 1-3 sản phẩm, mỗi món có lý do phù hợp |
| TC-04 | Price answer for active product | Active product resolved | User asks "giá bao nhiêu" | Bot trả lời giá trực tiếp theo active product, đồng thời giữ promo/inventory theo hướng conservative |
| TC-05 | Shipping answer stays conservative | `shipping_policy_confirmed=false` | User asks ship/freeship | Bot dùng phrasing tạm tính / "theo dữ liệu em đang thấy"; không khẳng định chắc |
| TC-06 | Summary can mark shipping as ready | Final summary is being rendered | Bot builds final summary | Summary có ship/phí ship trong bản tóm tắt cuối và dùng gate xác nhận trước draft |
| TC-08 | Ambiguous product reference | `candidate_products` contains 2+ items | User: "lấy 2 sản phẩm đó" | Bot hỏi lại để xác nhận đúng combo |
| TC-09 | No auto-pick on ambiguity | `candidate_products` contains 2+ items | User: "món kia nhé" | Bot không tự pick item gần nhất |
| TC-10 | Active product stability across policy Q&A | `active_product=mask`, policy questions follow | User asks price, promo, shipping in sequence | Bot vẫn bám đúng `active_product`; không drift sang món khác |
| TC-11 | Returning customer initial claim | `customer_verified=false` | User: "trước tôi mua rồi mà" | Bot xin số điện thoại để tra cứu trước |
| TC-12 | Returning customer matched profile | Valid profile found from phone | User provides phone | Bot tóm tắt contact cũ và yêu cầu xác nhận |
| TC-13 | Partial remembered contact | Profile has only phone or only address | Bot enters contact confirmation flow | Bot hỏi phần còn thiếu; không fail im lặng |
| TC-14 | Reuse old contact without consent blocked | Old profile exists | User has not confirmed old contact | Bot không tự tạo đơn bằng thông tin cũ |
| TC-15 | Cart confirmation before checkout | Buy intent but cart ambiguous | User wants to buy with ambiguous references | Bot clarify cart before asking final receiver info |
| TC-16 | Cart edit during checkout | `cart_items` already set | User changes one item mid-flow | Bot rebuild summary và xin xác nhận lại |
| TC-17 | No silent cart merge | Existing cart + new ambiguous edit | User says short deictic edit | Bot không merge theo suy đoán |
| TC-18 | Final confirmation gate | Cart and receiver info complete | Bot ready to finalize | Bot render summary đầy đủ trước khi tạo draft |
| TC-19 | Missing phone blocks final confirmation | `phone_verified=false` | User wants to chốt đơn | Bot thu số điện thoại trước; không nhảy sang tạo draft |
| TC-20 | Missing address blocks final confirmation | `address_confirmed=false` | User wants to chốt đơn | Bot thu/xác nhận địa chỉ trước |
| TC-21 | Final summary completeness | All fields ready | Bot renders final summary | Summary có đủ item, quantity, tiền hàng, shipping, total, phone, address |
| TC-22 | Follow-up on summary should resend summary | Summary shown but user asks "thông tin nào?" | User asks follow-up instead of confirming | Bot resend/tóm tắt lại summary; chưa tạo draft order |
| TC-23 | Explicit confirmation required for draft | Summary shown and user confirms | User: "Ừ, chốt giúp tôi" | Bot chỉ tạo draft sau xác nhận rõ ràng |
| TC-24 | Remembered contact pending blocks draft | Old contact shown, user not confirmed yet | Buy intent present | Bot không tạo draft order |
| TC-25 | Natural closure wording | Draft/order created | Bot sends closing response | Bot nêu trạng thái rõ ràng, không dùng câu sai trạng thái hoặc mã kỹ thuật thô |

## Suggested Test Data Matrix

| Variable | Variants |
|---|---|
| Customer profile | new customer / verified returning customer / partial remembered contact |
| Product context | single active product / multiple candidate products / missing product context recovered from history |
| Fact grounding | confirmed / unconfirmed |
| Cart state | empty / ambiguous / confirmed / edited during checkout |
| Contact state | missing / partial / fully confirmed |
| Confirmation state | not asked / asked / explicitly confirmed / explicitly corrected |

## Regression Priorities

Ưu tiên regression test cho các vùng dễ lỗi nhất:

1. ambiguity resolution cho `2 sản phẩm đó`, `cái kia`, `món đó`
2. active product drift khi hỏi giá / ship / combo / quà tặng
3. returning customer confirmation flow
4. partial remembered contact flow
5. final confirmation gate trước draft creation
6. cart edit trong lúc checkout

## Definition of Done

Một thay đổi liên quan sales bot chỉ được coi là done khi:

- tất cả acceptance criteria liên quan đã được đáp ứng
- test cases trọng yếu đã được cover
- không có hành vi bịa fact khi thiếu grounding
- không có hành vi auto-pick sai sản phẩm khi khách nói mơ hồ
- không có hành vi tạo draft quá sớm khi chưa confirm contact/cart/summary

## References

- `docs/sales-bot-technical-decision-table-and-pseudocode.md`
- `docs/sales-bot-operating-rules-and-prompt.md`
- `docs/code-standards.md`
