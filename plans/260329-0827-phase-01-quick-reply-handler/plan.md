---
title: "Phase 1: Quick Reply Handler Implementation"
description: "Facebook Messenger Quick Reply/Postback handler with product mapping, gift selection, and freeship logic"
status: pending
priority: P1
effort: 16h
branch: master
tags: [messenger, webhook, quick-reply, postback, phase-1]
created: 2026-03-29
---

# Phase 1: Quick Reply Handler - Implementation Plan

**Duration:** 3 days (16 hours)
**Cost:** 12M VND
**Status:** ❌ Not Started
**Priority:** CRITICAL

---

## Overview

Implement Facebook Messenger Quick Reply and Postback handler for 3 product buttons from Facebook Ads:
- "Tôi muốn mua Kem Chống Nắng nhé Múi ơi" → KCN
- "Tôi muốn mua Kem Lụa nhé Múi ơi" → KL
- "Tôi muốn mua 2 sản phẩm để được freeship nhé Múi ơi" → COMBO_2

---

## Architecture Overview

```
Facebook Webhook → WebhookProcessor → QuickReplyHandler
                                           ↓
                                    ProductMappingService
                                           ↓
                                    GiftSelectionService
                                           ↓
                                    FreeshipCalculator
                                           ↓
                                    MessengerApiService
```

---

## Data Flow

### Input: Quick Reply Event
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

### Input: Postback Event
```json
{
  "object": "page",
  "entry": [{
    "messaging": [{
      "sender": {"id": "123456789"},
      "postback": {
        "title": "Tôi muốn mua Kem Chống Nắng",
        "payload": "PRODUCT_KCN"
      }
    }]
  }]
}
```

### Output: Response Message
```
✨ Dạ em xin phép gửi chị thông tin:

📦 Sản phẩm: Kem Chống Nắng SPF50+ (350.000đ)
🎁 Quà tặng: Sữa rửa mặt mini 50ml

Tổng cộng: 350.000đ (Miễn phí vận chuyển)

Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕
```

---

## Implementation Phases

### Phase 1.1: Models & Database (4h)
- [ ] Create QuickReply model
- [ ] Update Message model to include QuickReply
- [ ] Create Gift entity
- [ ] Create ProductGiftMapping entity
- [ ] Generate EF Core migration
- [ ] Seed sample data (3 products, 5 gifts, mappings)

### Phase 1.2: Services Layer (6h)
- [ ] Create IProductMappingService + implementation
- [ ] Create IGiftSelectionService + implementation
- [ ] Create IFreeshipCalculator + implementation
- [ ] Create QuickReplyHandler service
- [ ] Update WebhookProcessor to route Quick Reply/Postback

### Phase 1.3: Testing (4h)
- [ ] Unit tests: ProductMappingService (>90% coverage)
- [ ] Unit tests: GiftSelectionService (>90% coverage)
- [ ] Unit tests: FreeshipCalculator (>90% coverage)
- [ ] Unit tests: QuickReplyHandler (>90% coverage)
- [ ] Integration tests: End-to-end Quick Reply flow
- [ ] Integration tests: End-to-end Postback flow

### Phase 1.4: Documentation & Review (2h)
- [ ] Update system-architecture.md
- [ ] Update code-standards.md
- [ ] Code review with checklist
- [ ] Manual testing with real Facebook payload

---

## File Structure

```
src/MessengerWebhook/
├── Models/
│   └── QuickReply.cs (NEW)
├── Data/
│   ├── Entities/
│   │   ├── Gift.cs (NEW)
│   │   └── ProductGiftMapping.cs (NEW)
│   ├── Repositories/
│   │   ├── IGiftRepository.cs (NEW)
│   │   ├── GiftRepository.cs (NEW)
│   │   ├── IProductGiftMappingRepository.cs (NEW)
│   │   └── ProductGiftMappingRepository.cs (NEW)
│   └── Migrations/
│       └── YYYYMMDDHHMMSS_AddGiftsAndMappings.cs (NEW)
├── Services/
│   ├── ProductMapping/
│   │   ├── IProductMappingService.cs (NEW)
│   │   └── ProductMappingService.cs (NEW)
│   ├── GiftSelection/
│   │   ├── IGiftSelectionService.cs (NEW)
│   │   └── GiftSelectionService.cs (NEW)
│   ├── Freeship/
│   │   ├── IFreeshipCalculator.cs (NEW)
│   │   └── FreeshipCalculator.cs (NEW)
│   └── QuickReply/
│       ├── IQuickReplyHandler.cs (NEW)
│       └── QuickReplyHandler.cs (NEW)
└── WebhookProcessor.cs (UPDATE)

tests/MessengerWebhook.UnitTests/
├── Services/
│   ├── ProductMappingServiceTests.cs (NEW)
│   ├── GiftSelectionServiceTests.cs (NEW)
│   ├── FreeshipCalculatorTests.cs (NEW)
│   └── QuickReplyHandlerTests.cs (NEW)

tests/MessengerWebhook.IntegrationTests/
└── QuickReplyFlowTests.cs (NEW)
```

---

## Database Schema

### New Tables

```sql
-- Gifts table
CREATE TABLE "Gifts" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Code" varchar(50) UNIQUE NOT NULL,
    "Name" varchar(200) NOT NULL,
    "Description" text,
    "ImageUrl" varchar(500),
    "IsActive" boolean DEFAULT true,
    "CreatedAt" timestamp DEFAULT NOW(),
    "UpdatedAt" timestamp DEFAULT NOW()
);

-- ProductGiftMappings table
CREATE TABLE "ProductGiftMappings" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductCode" varchar(50) NOT NULL,
    "GiftCode" varchar(50) NOT NULL,
    "Priority" int DEFAULT 0,
    "CreatedAt" timestamp DEFAULT NOW(),
    FOREIGN KEY ("ProductCode") REFERENCES "Products"("Code"),
    FOREIGN KEY ("GiftCode") REFERENCES "Gifts"("Code")
);

-- Indexes
CREATE INDEX "IX_ProductGiftMappings_ProductCode" ON "ProductGiftMappings"("ProductCode");
CREATE INDEX "IX_ProductGiftMappings_GiftCode" ON "ProductGiftMappings"("GiftCode");
```

### Sample Data

```sql
-- Products (assuming these exist or will be created)
INSERT INTO "Products" ("Id", "Code", "Name", "BasePrice") VALUES
('prod-kcn', 'KCN', 'Kem Chống Nắng SPF50+', 350000),
('prod-kl', 'KL', 'Kem Lụa Dưỡng Ẩm', 280000),
('prod-combo', 'COMBO_2', 'Combo 2 Sản Phẩm', 600000);

-- Gifts
INSERT INTO "Gifts" ("Id", "Code", "Name", "Description") VALUES
('gift-01', 'GIFT_SRM_MINI', 'Sữa rửa mặt mini 50ml', 'Làm sạch sâu, dịu nhẹ'),
('gift-02', 'GIFT_TONER_MINI', 'Toner cân bằng mini 30ml', 'Cân bằng pH da'),
('gift-03', 'GIFT_MASK', 'Mặt nạ dưỡng da', 'Cấp ẩm chuyên sâu'),
('gift-04', 'GIFT_SERUM_SAMPLE', 'Serum dưỡng da sample 5ml', 'Dưỡng trắng, chống lão hóa'),
('gift-05', 'GIFT_LIPBALM', 'Son dưỡng môi SPF15', 'Dưỡng ẩm, chống nắng cho môi');

-- Mappings
INSERT INTO "ProductGiftMappings" ("ProductCode", "GiftCode", "Priority") VALUES
('KCN', 'GIFT_SRM_MINI', 1),
('KCN', 'GIFT_TONER_MINI', 2),
('KL', 'GIFT_MASK', 1),
('KL', 'GIFT_SERUM_SAMPLE', 2),
('COMBO_2', 'GIFT_LIPBALM', 1);
```

---

## Service Contracts

### IProductMappingService
```csharp
public interface IProductMappingService
{
    Task<Product?> GetProductByPayloadAsync(string payload);
    Task<Product?> GetProductByCodeAsync(string code);
    bool IsValidPayload(string payload);
}
```

### IGiftSelectionService
```csharp
public interface IGiftSelectionService
{
    Task<Gift?> SelectGiftForProductAsync(string productCode);
    Task<List<Gift>> GetAvailableGiftsForProductAsync(string productCode);
}
```

### IFreeshipCalculator
```csharp
public interface IFreeshipCalculator
{
    bool IsEligibleForFreeship(List<string> productCodes);
    decimal CalculateShippingFee(List<string> productCodes);
    string GetFreeshipMessage(bool isEligible);
}
```

### IQuickReplyHandler
```csharp
public interface IQuickReplyHandler
{
    Task<string> HandleQuickReplyAsync(string senderId, string payload);
    Task<string> HandlePostbackAsync(string senderId, string payload);
}
```

---

## Business Logic

### Product Mapping Rules
```
Payload Format: "PRODUCT_{CODE}"
- "PRODUCT_KCN" → Product.Code = "KCN"
- "PRODUCT_KL" → Product.Code = "KL"
- "PRODUCT_COMBO_2" → Product.Code = "COMBO_2"
```

### Gift Selection Rules
```
1. Query ProductGiftMappings by ProductCode
2. Order by Priority ASC
3. Select first active gift
4. If no mapping found → return null (no gift)
```

### Freeship Rules
```
Condition: productCodes.Count >= 2 OR productCode == "COMBO_2"
Shipping Fee: 30,000 VND (if not eligible)
Message:
  - Eligible: "(Miễn phí vận chuyển)"
  - Not Eligible: "(Phí vận chuyển: 30.000đ)"
```

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Facebook payload format differs from docs | HIGH | MEDIUM | Request sample webhook payload from client before implementation |
| Product/Gift data not available | HIGH | HIGH | Use mock data for development, request real data ASAP |
| Freeship logic unclear | MEDIUM | HIGH | Clarify with client: >= 2 items or >= 2 quantity? |
| Performance: DB queries per message | LOW | LOW | Add caching layer for products/gifts (future optimization) |
| Duplicate webhook events | MEDIUM | LOW | Already handled by WebhookProcessor idempotency check |

---

## Backwards Compatibility

- Existing message handling flow unchanged
- WebhookProcessor already handles Postback events (currently just logs)
- New Quick Reply handling is additive, no breaking changes
- Database migration is additive (new tables only)

---

## Test Matrix

### Unit Tests (>90% coverage)

**ProductMappingService:**
- ✅ Valid payload → returns product
- ✅ Invalid payload → returns null
- ✅ Product not found → returns null
- ✅ Case insensitive payload matching

**GiftSelectionService:**
- ✅ Product with gift mapping → returns gift
- ✅ Product without mapping → returns null
- ✅ Multiple mappings → returns highest priority
- ✅ Inactive gift → skips to next priority

**FreeshipCalculator:**
- ✅ Single product → not eligible
- ✅ Two products → eligible
- ✅ COMBO_2 product → eligible
- ✅ Shipping fee calculation

**QuickReplyHandler:**
- ✅ Valid Quick Reply → returns formatted message
- ✅ Valid Postback → returns formatted message
- ✅ Invalid payload → returns error message
- ✅ Product not found → returns error message
- ✅ Message formatting with/without gift
- ✅ Message formatting with/without freeship

### Integration Tests

**QuickReplyFlowTests:**
- ✅ End-to-end Quick Reply flow (KCN)
- ✅ End-to-end Postback flow (KL)
- ✅ COMBO_2 with freeship
- ✅ Invalid payload handling
- ✅ Database queries execute correctly
- ✅ Response sent via MessengerApiService

---

## Rollback Plan

### If Phase 1.1 fails (Database):
- Revert migration: `dotnet ef database update PreviousMigration`
- No code changes needed (new files not referenced yet)

### If Phase 1.2 fails (Services):
- Remove service registrations from Program.cs
- WebhookProcessor falls back to existing Postback handler (logs only)
- No data corruption (read-only operations)

### If Phase 1.3 fails (Tests):
- Fix tests before merging (non-blocking for development)
- Do not deploy to production until tests pass

### If Phase 1.4 fails (Production):
- Feature flag: disable Quick Reply routing in WebhookProcessor
- Fallback to existing message handling
- Monitor logs for errors, fix and redeploy

---

## Success Criteria

- [ ] Quick Reply events parsed correctly
- [ ] Postback events parsed correctly
- [ ] Product mapping works for all 3 buttons
- [ ] Gift selection returns correct gift
- [ ] Freeship calculation accurate
- [ ] Response message format matches spec
- [ ] Response time < 2s (measured in integration tests)
- [ ] Unit test coverage > 90%
- [ ] Integration tests pass
- [ ] Manual test with real Facebook webhook succeeds
- [ ] Code review approved
- [ ] Documentation updated

---

## Dependencies

**Blockers:** None (Phase 1 is foundation)

**Required from Client:**
1. Sample webhook payload from Facebook Ads (CRITICAL)
2. Product data: 3 products with codes, names, prices
3. Gift data: 5 gifts with codes, names, descriptions
4. Confirmation: Freeship threshold (>= 2 items?)
5. Confirmation: Shipping fee if not eligible (30k VND?)

**External Dependencies:**
- Facebook Messenger API (already integrated)
- PostgreSQL database (already setup)
- EF Core (already configured)

---

## Next Phase

After Phase 1 complete → **Phase 2: Nobita API Integration**
- Use product/gift data from Phase 1
- Create draft order with selected products
- Check customer risk score
- Collect phone number and address

---

## TODO Checklist

### Phase 1.1: Models & Database (4h)
- [ ] Create `Models/QuickReply.cs`
- [ ] Update `Models/Message.cs` to include `QuickReply? QuickReply`
- [ ] Create `Data/Entities/Gift.cs`
- [ ] Create `Data/Entities/ProductGiftMapping.cs`
- [ ] Create `Data/Repositories/IGiftRepository.cs`
- [ ] Create `Data/Repositories/GiftRepository.cs`
- [ ] Create `Data/Repositories/IProductGiftMappingRepository.cs`
- [ ] Create `Data/Repositories/ProductGiftMappingRepository.cs`
- [ ] Update `Data/MessengerBotDbContext.cs` to include new entities
- [ ] Generate migration: `dotnet ef migrations add AddGiftsAndMappings`
- [ ] Create seed data script
- [ ] Apply migration: `dotnet ef database update`

### Phase 1.2: Services Layer (6h)
- [ ] Create `Services/ProductMapping/IProductMappingService.cs`
- [ ] Create `Services/ProductMapping/ProductMappingService.cs`
- [ ] Create `Services/GiftSelection/IGiftSelectionService.cs`
- [ ] Create `Services/GiftSelection/GiftSelectionService.cs`
- [ ] Create `Services/Freeship/IFreeshipCalculator.cs`
- [ ] Create `Services/Freeship/FreeshipCalculator.cs`
- [ ] Create `Services/QuickReply/IQuickReplyHandler.cs`
- [ ] Create `Services/QuickReply/QuickReplyHandler.cs`
- [ ] Update `Services/WebhookProcessor.cs` to route Quick Reply
- [ ] Update `Services/WebhookProcessor.cs` to route Postback
- [ ] Register services in `Program.cs`

### Phase 1.3: Testing (4h)
- [ ] Create `tests/UnitTests/Services/ProductMappingServiceTests.cs`
- [ ] Create `tests/UnitTests/Services/GiftSelectionServiceTests.cs`
- [ ] Create `tests/UnitTests/Services/FreeshipCalculatorTests.cs`
- [ ] Create `tests/UnitTests/Services/QuickReplyHandlerTests.cs`
- [ ] Create `tests/IntegrationTests/QuickReplyFlowTests.cs`
- [ ] Run all tests: `dotnet test`
- [ ] Verify coverage: `dotnet test /p:CollectCoverage=true`

### Phase 1.4: Documentation & Review (2h)
- [ ] Update `docs/system-architecture.md` with Quick Reply flow
- [ ] Update `docs/code-standards.md` with service patterns
- [ ] Code review checklist
- [ ] Manual test with sample Facebook payload
- [ ] Update `plans/260327-2102-mui-xu-cosmetics-implementation/phase-01-quick-reply-handler.md` status

---

## Unresolved Questions

1. **CRITICAL:** What is the exact payload format from Facebook Ads buttons?
   - Quick Reply or Postback?
   - Payload string format: `PRODUCT_KCN` or `{"type":"product","code":"KCN"}`?

2. **HIGH:** Freeship threshold clarification
   - >= 2 distinct products OR >= 2 total quantity?
   - Example: 2x KCN = freeship?

3. **HIGH:** Shipping fee amount
   - 30,000 VND or different amount?
   - Same for all regions or varies by location?

4. **MEDIUM:** Gift selection logic
   - Always auto-select or let customer choose?
   - If multiple gifts available, show options or pick highest priority?

5. **MEDIUM:** Product data availability
   - When can we get real product data (codes, names, prices)?
   - When can we get real gift data?

6. **LOW:** Response message format
   - Current format acceptable?
   - Need product images in response?
