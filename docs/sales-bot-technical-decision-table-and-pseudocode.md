# Sales Bot Technical Decision Table and Pseudocode

## Overview

Tài liệu này chuyển bộ quy tắc vận hành bot sang dạng kỹ thuật hơn để đội dev có thể map trực tiếp vào state machine, routing logic, runtime guards, và response generation.

## Canonical Runtime State

```text
active_need: string | null
active_product: ProductRef | null
candidate_products: ProductRef[]
cart_items: CartItem[]
customer_verified: boolean
remembered_contact_available: boolean
phone_verified: boolean
address_confirmed: boolean
price_confirmed: boolean
promotion_confirmed: boolean
shipping_policy_confirmed: boolean
inventory_confirmed: boolean
order_status_confirmed: boolean
final_price_summary_ready: boolean
order_status: DISCOVER_NEED | CLARIFY_NEED | RECOMMEND_PRODUCTS | PRODUCT_DETAIL | ANSWER_POLICY_FACT | CLARIFY_CART | VERIFY_RETURNING_CUSTOMER | COLLECT_CUSTOMER_INFO | FINAL_CONFIRMATION | DRAFT_ORDER_CREATED | ORDER_CONFIRMED
```

## Decision Table

| Condition | Additional Check | Required Action | Forbidden Action |
|---|---|---|---|
| User only greets | No verified customer profile | Move to need discovery | Pretend returning customer |
| User expresses vague need | Need lacks actionable detail | Ask 1 clarifying question | Recommend 3+ unrelated products |
| User asks product detail | Active product exists | Answer detail for active product | Drift to another product |
| User asks price | Active product resolved | Answer product price directly; keep promo/inventory conservative unless separately confirmed | Over-assert promo/inventory together with price |
| User asks shipping/promo | Shipping/promo not separately confirmed | Use safe phrasing and mark as provisional | State business fact as certain |
| User says “2 sản phẩm đó” | Recent history still yields 2+ plausible products | Clarify product reference | Auto-pick most recent mention |
| User says bought before | `customer_verified == false` | Request phone for lookup | Act as if old profile already known |
| Returning customer found | Contact record exists | Read back compact summary and ask confirm | Auto-use old address without consent |
| Buy intent detected | Cart ambiguous | Ask cart confirmation | Create draft order |
| Buy intent detected | Cart resolved but receiver info missing | Collect missing customer info | Mark order confirmed |
| Cart + pricing + contact ready | Final summary ready | Ask final confirmation | Skip final confirmation |
| Final confirmation received | Validation complete | Create draft or confirm order | Ask for same info again |
| Customer edits item during checkout | Cart already exists | Rebuild summary and re-confirm | Merge silently without explicit confirmation |

## Response Policy Table

| Response Type | Preconditions | Output Pattern |
|---|---|---|
| Greeting | No need yet | greet + one discovery question |
| Clarification | Need or cart ambiguous | short confirm question |
| Recommendation | Need understood | 1-3 products + concrete reason |
| Fact answer | Fact grounded | concise direct answer |
| Safe fact answer | Fact ungrounded | verification phrasing |
| Returning customer lookup | Bought-before signal | request phone for lookup |
| Contact confirmation | Profile found | summarize remembered contact + ask confirm |
| Final confirmation | Cart and contact ready | itemized summary + ship + total + receiver info |
| Draft created | Final confirm complete | acknowledge draft + explain next step |

## Runtime Guards

### Guard 1: business fact grounding

```pseudo
function canAssertPrice(ctx):
  return ctx.price_confirmed == true

function canAssertPromotion(ctx):
  return ctx.promotion_confirmed == true

function canAssertShipping(ctx):
  return ctx.shipping_policy_confirmed == true

function canAssertInventory(ctx):
  return ctx.inventory_confirmed == true
```

### Guard 2: returning customer claims

```pseudo
function canUseReturningCustomerLanguage(ctx):
  return ctx.customer_verified == true
```

If false, reject phrases implying prior verified customer knowledge.

### Guard 3: ambiguous product reference

```pseudo
function mustClarifyReference(ctx, userMessage):
  return referencesProductDeictically(userMessage)
    and len(recentHistoryProductCandidates(ctx)) > 1
```

Runtime hiện không chỉ dựa vào `candidate_products` thuần túy. Guard này build candidate từ history gần đây của cả user và assistant; nếu sau bước này vẫn còn hơn 1 sản phẩm hợp lý thì bot phải hỏi lại thay vì auto-pick.

### Guard 4: order creation gate

```pseudo
function canProceedToFinalConfirmation(ctx):
  return len(ctx.cart_items) > 0
    and ctx.phone_verified == true
    and ctx.address_confirmed == true

function canCreateDraftOrder(ctx):
  return ctx.final_price_summary_ready == true
    and ctx.awaiting_final_summary_confirmation == true
    and userExplicitlyConfirmedSummary() == true
```

`final_price_summary_ready` là cờ sau khi bot đã render summary cuối. Runtime hiện dùng nó như gate để tạo draft, không phải điều kiện để bắt đầu render summary.

## State Transition Pseudocode

```pseudo
onIncomingMessage(ctx, userMessage):
  intent = classifyIntent(userMessage, ctx)

  if isGreeting(intent):
    ctx.order_status = DISCOVER_NEED
    return greetAndAskNeed()

  if signalsReturningCustomer(userMessage) and ctx.customer_verified == false:
    ctx.order_status = VERIFY_RETURNING_CUSTOMER
    return askPhoneForLookup()

  if ctx.order_status == VERIFY_RETURNING_CUSTOMER and containsPhone(userMessage):
    profile = lookupCustomerByPhone(userMessage)
    if profile.exists:
      ctx.customer_verified = true
      ctx.remembered_contact_available = true
      return summarizeProfileAndAskConfirmation(profile)
    return askForFullContactInfo()

  if needIsUnclear(ctx, userMessage):
    ctx.order_status = CLARIFY_NEED
    return askOneClarifyingQuestion()

  if asksPolicyFact(intent):
    ctx.order_status = ANSWER_POLICY_FACT
    return answerFactSafely(ctx, userMessage)

  if indicatesProductInterest(intent):
    ctx.active_product = resolveProduct(ctx, userMessage)
    ctx.order_status = PRODUCT_DETAIL
    return answerProductDetail(ctx.active_product)

  if indicatesBuyIntent(intent):
    if mustClarifyReference(ctx, userMessage):
      ctx.order_status = CLARIFY_CART
      return clarifyCartItems(ctx)

    updateCart(ctx, userMessage)

    if missingContactInfo(ctx):
      ctx.order_status = COLLECT_CUSTOMER_INFO
      return collectMissingContactInfo(ctx)

    if canProceedToFinalConfirmation(ctx):
      ctx.order_status = FINAL_CONFIRMATION
      return renderFinalConfirmation(ctx)

  if awaitingFinalSummaryConfirmation(ctx) and userAsksFollowUpAboutSummary(userMessage):
    return renderFinalConfirmation(ctx)

  if awaitingFinalSummaryConfirmation(ctx) and userExplicitlyConfirmedSummary():
    draft = createDraftOrder(ctx)
    ctx.order_status = DRAFT_ORDER_CREATED
    return acknowledgeDraftOrder(draft)

  return fallbackHelpfully(ctx)
```

## Fact Answer Strategy

```pseudo
function answerFactSafely(ctx, userMessage):
  if asksPrice(userMessage):
    return directPriceAnswer(ctx.active_product)
    // runtime hiện set price_confirmed=true khi đã resolve được active product
    // nhưng vẫn giữ promotion/inventory ở trạng thái conservative

  if asksPromotion(userMessage):
    if canAssertPromotion(ctx):
      return directPromotionAnswer(ctx.active_product)
    return verificationPromotionAnswer()

  if asksShipping(userMessage):
    if canAssertShipping(ctx):
      return directShippingAnswer(ctx)
    return provisionalShippingAnswer(ctx)
```

Lưu ý: ở flow price/shipping thông thường, runtime hiện test theo hướng conservative: trả giá trực tiếp cho sản phẩm đang active, nhưng vẫn nói promo/ship theo kiểu tạm tính hoặc "đang thấy" cho đến khi summary cuối được dựng.

## Recommended Clarification Templates

### Product disambiguation

```text
Dạ để em xác nhận cho đúng nhé: chị đang muốn lấy [option A] hay [option B] ạ?
```

### Returning customer lookup

```text
Dạ nếu chị từng mua rồi, chị gửi em số điện thoại đặt hàng trước đây để em đối chiếu lại thông tin cho nhanh nhé.
```

### Final order confirmation

```text
Dạ em xác nhận đơn của chị như sau:
1. [product 1] - [price]
2. [product 2] - [price]
Tổng tạm tính: [total]
[shipping summary]
Giao về: [address] - SĐT [masked phone]
Chị xác nhận giúp em thông tin này đúng để em chốt đơn hoàn tất nhé.
```

## Anti-Patterns

- Auto-select latest mentioned product when reference remains ambiguous.
- Use remembered contact data without explicit customer confirmation.
- Emit hard business facts while fact flags remain unverified.
- Create draft order before final summary confirmation.
- Let active product drift during shipping/policy Q&A.

## Implementation Priorities

1. Fact grounding guards
2. Ambiguity resolution guard
3. Returning-customer verification gate
4. Final confirmation gate
5. Transcript regression coverage for cart edits and remembered contact flows

## References

- `docs/sales-bot-operating-rules-and-prompt.md`
- `docs/code-standards.md`
- `docs/facebook-messenger-salesbot-plan.md`
