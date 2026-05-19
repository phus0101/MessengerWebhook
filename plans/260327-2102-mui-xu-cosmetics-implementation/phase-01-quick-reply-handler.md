# Phase 1: Quick Reply Handler - Technical Specification

**Phase:** 1/7
**Duration:** 3 days
**Cost:** 12M VND
**Status:** ❌ Not Started
**Dependencies:** None
**Priority:** CRITICAL

---

## Overview

Implement Facebook Messenger Quick Reply button handler để xử lý 3 buttons từ Facebook Ads:
- "Tôi muốn mua Kem Chống Nắng nhé Múi ơi"
- "Tôi muốn mua Kem Lụa nhé Múi ơi"
- "Tôi muốn mua 2 sản phẩm để được freeship nhé Múi ơi"

---

## Current Status

### ✅ Có sẵn
- Webhook endpoint (`/webhook` POST)
- WebhookEvent model parsing
- Message event handling

### ❌ Thiếu
- Quick Reply payload parsing
- Postback event handling
- Product mapping logic
- Gift selection logic
- Freeship calculation

---

## Requirements

### Functional Requirements

1. **Parse Quick Reply/Postback**
   - Detect Quick Reply trong message event
   - Detect Postback event
   - Extract payload từ cả 2 loại

2. **Product Mapping**
   - Map button text/payload → product code
   - Validate product tồn tại trong database
   - Load product details (name, price, image)

3. **Gift Selection**
   - Tự động chọn quà tặng theo logic
   - Validate gift tồn tại trong database
   - Load gift details

4. **Freeship Logic**
   - Check điều kiện freeship (>= 2 products)
   - Calculate shipping fee
   - Apply freeship discount

5. **Response Format**
   ```
   ✨ Dạ em xin phép gửi chị thông tin:

   📦 Sản phẩm: [Product Name] ([Price]đ)
   🎁 Quà tặng: [Gift Name]

   Tổng cộng: [Total]đ (Miễn phí vận chuyển)

   Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕
   ```

### Non-Functional Requirements

- Response time: < 2s
- Handle concurrent requests
- Graceful error handling
- Logging for debugging

---

## Research Needed

### 1. Facebook API Documentation ✅ DONE
- [Quick Reply format](https://developers.facebook.com/docs/messenger-platform/send-messages/quick-replies/)
- [Postback format](https://developers.facebook.com/docs/messenger-platform/reference/webhook-events/messaging_postbacks)

**Finding:**
```json
// Quick Reply
{
  "message": {
    "text": "Tôi muốn mua Kem Chống Nắng",
    "quick_reply": {
      "payload": "PRODUCT_KCN"
    }
  }
}

// Postback
{
  "postback": {
    "title": "Tôi muốn mua Kem Chống Nắng",
    "payload": "PRODUCT_KCN"
  }
}
```

### 2. Product & Gift Data Structure
- Cần file Excel/CSV với 10 sản phẩm chính
- Cần file Excel/CSV với 20 mã quà tặng
- Mapping rules: product → gift

---

## Questions for Client

### CRITICAL (Blocking)

1. **Button Payload Format**
   - Facebook Ads đang dùng Quick Reply hay Postback?
   - Payload format: `PRODUCT_KCN` hay `{"type":"product","code":"KCN"}`?
   - Có thể test được không? (gửi sample webhook payload)

2. **Product Mapping**
   ```
   "Tôi muốn mua Kem Chống Nắng" → Product code: KCN?
   "Tôi muốn mua Kem Lụa" → Product code: KL?
   "Tôi muốn mua 2 sản phẩm để được freeship" → Combo code: COMBO_2?
   ```

3. **Gift Selection Logic**
   - Tự động chọn hay khách chọn?
   - Logic: theo sản phẩm? giá trị đơn? chương trình KM? random?
   - Ví dụ: Mua KCN → tặng Gift A?

4. **Freeship Threshold**
   - "Mua 2 sản phẩm" = >= 2 items hay giá trị đơn >= X?
   - Freeship toàn quốc hay theo khu vực?
   - Phí ship nếu không đủ điều kiện: 30k? 50k?

### HIGH (Important)

5. **Product Data**
   - File Excel/CSV với 10 sản phẩm: code, name, price, description, image_url?
   - File Excel/CSV với 20 quà tặng: code, name, description?

6. **Response Format**
   - Format hiện tại OK không?
   - Cần thêm hình ảnh sản phẩm không?

---

## Solution Options

### Option 1: Support Both Quick Reply & Postback (RECOMMENDED)

**Approach:**
```csharp
// WebhookController.cs
if (messaging.Message?.QuickReply != null)
{
    await HandleQuickReply(messaging.Message.QuickReply.Payload);
}
else if (messaging.Postback != null)
{
    await HandlePostback(messaging.Postback.Payload);
}
```

**Pros:**
- Flexible, support cả 2 formats
- Future-proof nếu Facebook thay đổi
- Dễ test với cả 2 loại

**Cons:**
- Code phức tạp hơn một chút
- Cần test cả 2 paths

**Cost:** 12M VND
**Timeline:** 3 days

---

### Option 2: Quick Reply Only

**Approach:**
```csharp
// WebhookController.cs
if (messaging.Message?.QuickReply != null)
{
    await HandleQuickReply(messaging.Message.QuickReply.Payload);
}
```

**Pros:**
- Đơn giản hơn
- Ít code hơn

**Cons:**
- Không support Postback
- Phải refactor nếu Facebook Ads dùng Postback

**Cost:** 10M VND
**Timeline:** 2.5 days

---

### Option 3: Text Parsing (NOT RECOMMENDED)

**Approach:**
```csharp
if (message.Text.Contains("Kem Chống Nắng"))
{
    productCode = "KCN";
}
```

**Pros:**
- Không cần payload
- Work với bất kỳ format nào

**Cons:**
- Không chính xác (typo, variations)
- Phụ thuộc vào text
- Khó maintain

**Cost:** 8M VND
**Timeline:** 2 days

---

## Recommended Solution: Option 1

**Lý do:**
1. **Flexible:** Support cả Quick Reply lẫn Postback
2. **Accurate:** Dùng payload thay vì parse text
3. **Future-proof:** Không phụ thuộc vào button text
4. **Best practice:** Theo Facebook documentation

**Trade-offs:**
- Cost cao hơn Option 2 (12M vs 10M)
- Code phức tạp hơn một chút
- Nhưng đáng giá vì tính linh hoạt

---

## Implementation Plan

### Day 1: Setup & Models (4h)

**Tasks:**
1. Create `QuickReplyPayload` model
2. Create `PostbackPayload` model
3. Update `WebhookEvent` to include both
4. Create `ProductMapping` service
5. Create `GiftSelection` service

**Files:**
- `Models/Webhook/QuickReplyPayload.cs` (new)
- `Models/Webhook/PostbackPayload.cs` (new)
- `Services/ProductMappingService.cs` (new)
- `Services/GiftSelectionService.cs` (new)

**Cost:** 3M VND

---

### Day 2: Handler Logic (6h)

**Tasks:**
1. Implement `HandleQuickReply` method
2. Implement `HandlePostback` method
3. Implement product lookup
4. Implement gift selection
5. Implement freeship calculation
6. Format response message

**Files:**
- `Controllers/WebhookController.cs` (update)
- `Services/QuickReplyHandler.cs` (new)

**Cost:** 5M VND

---

### Day 3: Testing & Integration (6h)

**Tasks:**
1. Unit tests for payload parsing
2. Unit tests for product mapping
3. Unit tests for gift selection
4. Integration test with mock webhook
5. Manual test with real Facebook payload
6. Error handling & logging

**Files:**
- `tests/UnitTests/Services/QuickReplyHandlerTests.cs` (new)
- `tests/IntegrationTests/WebhookQuickReplyTests.cs` (new)

**Cost:** 4M VND

---

## Database Changes

### New Tables

```sql
-- Products table (already exists, may need updates)
CREATE TABLE IF NOT EXISTS "Products" (
    "Id" uuid PRIMARY KEY,
    "Code" varchar(50) UNIQUE NOT NULL,
    "Name" varchar(200) NOT NULL,
    "Price" decimal(18,2) NOT NULL,
    "Description" text,
    "ImageUrl" varchar(500),
    "IsActive" boolean DEFAULT true
);

-- Gifts table (new)
CREATE TABLE IF NOT EXISTS "Gifts" (
    "Id" uuid PRIMARY KEY,
    "Code" varchar(50) UNIQUE NOT NULL,
    "Name" varchar(200) NOT NULL,
    "Description" text,
    "IsActive" boolean DEFAULT true
);

-- ProductGiftMapping table (new)
CREATE TABLE IF NOT EXISTS "ProductGiftMappings" (
    "Id" uuid PRIMARY KEY,
    "ProductCode" varchar(50) NOT NULL,
    "GiftCode" varchar(50) NOT NULL,
    "Priority" int DEFAULT 0,
    FOREIGN KEY ("ProductCode") REFERENCES "Products"("Code"),
    FOREIGN KEY ("GiftCode") REFERENCES "Gifts"("Code")
);
```

---

## API Contracts

### Input: Quick Reply Webhook

```json
{
  "object": "page",
  "entry": [{
    "messaging": [{
      "sender": {"id": "123456789"},
      "message": {
        "text": "Tôi muốn mua Kem Chống Nắng",
        "quick_reply": {
          "payload": "PRODUCT_KCN"
        }
      }
    }]
  }]
}
```

### Output: Response Message

```json
{
  "recipient": {"id": "123456789"},
  "message": {
    "text": "✨ Dạ em xin phép gửi chị thông tin:\n\n📦 Sản phẩm: Kem Chống Nắng SPF50+ (350.000đ)\n🎁 Quà tặng: Sữa rửa mặt mini 50ml\n\nTổng cộng: 350.000đ (Miễn phí vận chuyển)\n\nChị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕"
  }
}
```

---

## Testing Strategy

### Unit Tests
- ✅ Parse Quick Reply payload
- ✅ Parse Postback payload
- ✅ Map payload to product code
- ✅ Select gift for product
- ✅ Calculate freeship
- ✅ Format response message

### Integration Tests
- ✅ End-to-end Quick Reply flow
- ✅ End-to-end Postback flow
- ✅ Invalid payload handling
- ✅ Product not found handling
- ✅ Gift not found handling

### Manual Tests
- ✅ Test với real Facebook webhook
- ✅ Test cả 3 buttons
- ✅ Verify response format
- ✅ Verify database queries

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Facebook API format khác với docs | HIGH | MEDIUM | Test với real payload trước khi implement |
| Product/Gift data chưa có | HIGH | HIGH | Mock data để develop, request real data ASAP |
| Freeship logic không rõ | MEDIUM | HIGH | Clarify với client trước khi implement |
| Performance issue với DB queries | LOW | LOW | Use caching, optimize queries |

---

## Success Criteria

- ✅ Support cả Quick Reply và Postback
- ✅ Correctly map 3 buttons to products
- ✅ Auto-select appropriate gift
- ✅ Calculate freeship correctly
- ✅ Response format match requirement
- ✅ Response time < 2s
- ✅ 100% unit test coverage
- ✅ Integration tests pass

---

## Deliverables

1. ✅ Quick Reply handler implementation
2. ✅ Postback handler implementation
3. ✅ Product mapping service
4. ✅ Gift selection service
5. ✅ Freeship calculation logic
6. ✅ Unit tests (>90% coverage)
7. ✅ Integration tests
8. ✅ Documentation (API contracts, setup guide)

---

## Next Phase

After Phase 1 complete → **Phase 2: Nobita API Integration**
- Use product/gift data from Phase 1
- Create draft order with selected products
- Check customer risk score
