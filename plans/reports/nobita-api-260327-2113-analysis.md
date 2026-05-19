# Nobita API Analysis - CRITICAL FINDINGS

**Date:** 2026-03-27
**Source:** Nobita public API.postman_collection.json

---

## API Overview

**Base URL:** `https://testing.ecrm.vn/public-api/v1`
**Auth:** Header `ApiKey: c2117fba-aaae-462f-bf6c-4c2bef2c3bfc`

---

## Available Endpoints

### 1. POST /orders - Tạo đơn hàng
```json
{
  "invoice": {
    "discount": 20000,
    "type": 1,
    "isDiscountPrice": true,
    "vat": 0,
    "total": 400000,
    "depositAmount": 0,
    "transferAmount": 0,
    "details": [{
      "productId": 1,
      "quantity": 1,
      "weight": 100,
      "price": 400000,
      "discount": 0,
      "isDiscountPrice": true
    }]
  },
  "customerName": "Dũng Hồ",
  "customerNotes": "Cho xem hàng",
  "customerPhoneNumber": "0347448953",
  "shippingAddress": "75 lê thánh tôn...",
  "weight": 100,
  "sourceName": "Bot Bán Hàng"
}
```

### 2. GET /product-types
Lấy danh sách loại sản phẩm (điện tử, điện lạnh, hàng hóa, dịch vụ)

### 3. GET /product-categories?typeId=1
Lấy danh sách nhóm sản phẩm theo loại

### 4. GET /products
Query params:
- `typeId`: loại sản phẩm
- `categoyIds[]`: nhóm sản phẩm
- `page`, `pagesize`: phân trang
- `search`, `searchBy`: tìm kiếm
- `excludeOutOfStock`: true/false
- `sortBy`: CreateDateAsc|CreateDateDesc|InventoryAsc|InventoryDesc

---

## ⚠️ CRITICAL GAPS

### Missing Endpoints (Yêu cầu ban đầu)

1. **❌ Check Customer (Khách cũ/VIP/Risk)**
   - Không có endpoint để check khách hàng
   - Không có risk scoring
   - Không có thông tin lịch sử mua hàng

2. **❌ Draft Order (Đơn nháp)**
   - Chỉ có `/orders` (tạo đơn thực)
   - KHÔNG có endpoint tạo đơn nháp
   - KHÔNG có endpoint review/approve đơn

3. **❌ Tag/Label Orders**
   - Không có cách gắn tag "HIGH_RISK"
   - Không có cách gắn "Cảnh báo đỏ"

---

## Impact on Requirements

### Requirement 1: Check tỷ lệ nhận hàng
**Status:** ❌ KHÔNG KHẢ THI với API hiện tại

**Yêu cầu gốc:**
> "AI check Nobita thấy khách này tỷ lệ nhận thấp/có tiền sử bom hàng, thì AI tự động gắn Tag/Cảnh báo đỏ"

**Thực tế:** API không có endpoint này

**Solutions:**
- **Option A:** Implement trong chatbot database
  - Lưu lịch sử đơn hàng của khách
  - Tính risk score locally
  - Tag trong chatbot, không phải Nobita

- **Option B:** Request Nobita team thêm endpoint
  - `GET /customers/check?phone=xxx`
  - Response: `{is_existing, total_orders, risk_score}`

### Requirement 2: Đơn nháp
**Status:** ❌ KHÔNG KHẢ THI với API hiện tại

**Yêu cầu gốc:**
> "Bắn data qua API Nobita để thành Đơn Nháp (có nhân viên ngồi check lại)"

**Thực tế:** Chỉ có `/orders` - tạo đơn thực luôn

**Solutions:**
- **Option A:** Lưu đơn nháp trong chatbot database
  - Table `DraftOrders` trong PostgreSQL
  - Admin panel để nhân viên review
  - Sau khi approve → call `/orders` API

- **Option B:** Request Nobita team thêm endpoint
  - `POST /orders/draft` - tạo đơn nháp
  - `POST /orders/draft/{id}/approve` - approve đơn

### Requirement 3: Khách VIP tone adjustment
**Status:** ⚠️ CÓ THỂ IMPLEMENT (workaround)

**Workaround:**
- Lưu lịch sử đơn hàng trong chatbot database
- Check số lượng đơn đã tạo qua API
- Nếu >= 3 đơn → coi là VIP
- Adjust tone trong system prompt

---

## Recommended Approach

### Phase 2A: Nobita Integration (Basic)
**Duration:** 3 days | **Cost:** 12M VND

**Scope:**
- ✅ Integrate `/products` API (sync products)
- ✅ Integrate `/orders` API (create order)
- ✅ Map chatbot products → Nobita productId
- ❌ Skip customer check (not available)
- ❌ Skip draft order (not available)

### Phase 2B: Local Draft Order System
**Duration:** 4 days | **Cost:** 16M VND

**Scope:**
- Create `DraftOrders` table in chatbot DB
- Save draft when collect phone + address
- Admin panel for review (basic)
- Approve → call Nobita `/orders` API

### Phase 2C: Local Customer Tracking
**Duration:** 2 days | **Cost:** 8M VND

**Scope:**
- Track orders created via chatbot
- Calculate local risk score
- Tag high-risk in chatbot DB
- VIP detection (>= 3 orders)

**Total Phase 2:** 9 days, 36M VND (vs original 5 days, 20M)

---

## Questions for Client (URGENT)

### ✅ ANSWERED (2026-03-27 21:26)

1. **Draft Order Workflow**
   - ✅ OK lưu đơn nháp trong chatbot database
   - ✅ Nhân viên review ở Admin Panel riêng
   - ✅ Email notification khi có đơn nháp mới

2. **Customer Check**
   - ✅ OK track locally trong chatbot

3. **Risk Scoring**
   - ✅ OK tính risk score trong chatbot
   - Logic: số đơn bom / tổng đơn, tỷ lệ thấp → đánh dấu

4. **Order Creation**
   - ❌ KHÔNG tạo đơn thực luôn
   - ✅ Workflow: Draft → Email → Review → Approve → Call Nobita `/orders`

---

## Updated Workflow

```
1. Bot collect phone + address
   ↓
2. Create draft order in chatbot DB
   ↓
3. Send email notification to sales staff
   ↓
4. Staff review in Admin Panel
   ↓
5. Staff approve → Call Nobita POST /orders
   ↓
6. Order created in Nobita
```

---

## Updated Timeline

| Phase | Original | Updated | Reason |
|-------|----------|---------|--------|
| 2. Nobita API | 5 days, 20M | 9 days, 36M | Need local draft + tracking |
| **Total** | 35 days, 140M | 39 days, 156M | +4 days, +16M |

---

## Next Steps

1. **Clarify with client** (URGENT)
   - Send questions above
   - Get approval for local draft order approach

2. **Update Phase 2 spec**
   - Split into 2A, 2B, 2C
   - Update implementation plan

3. **Consider alternatives**
   - Request Nobita team add missing endpoints?
   - Use different CRM system?
