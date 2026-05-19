# Phase 2: Nobita API Integration - Technical Specification

**Duration:** 9 days (UPDATED) | **Cost:** 36M VND (UPDATED) | **Status:** ❌ Not Started

---

## ⚠️ CRITICAL UPDATE (2026-03-27 21:26)

**Client confirmed:**
- ✅ Draft order in chatbot DB (not Nobita)
- ✅ Admin panel for staff review
- ✅ Email notification on new draft
- ✅ Workflow: Draft → Email → Review → Approve → Nobita API

**Workflow:**
```
Bot → Draft Order (chatbot DB) → Email → Staff Review (Admin Panel) → Approve → POST /orders (Nobita)
```

---

## Implementation Plan (UPDATED)

### Phase 2A: Nobita API Basic (3 days, 12M)
- Sync products from `/products` API
- Map chatbot products → Nobita productId
- Implement `POST /orders` call (after approval)

### Phase 2B: Draft Order + Email (5 days, 20M) [UPDATED]
- Create `DraftOrders` table
- Email service (SMTP)
- Email template with draft details
- Admin panel (basic review UI)

### Phase 2C: Customer Tracking (2 days, 8M)
- Track order history
- Calculate risk score (bom rate)
- VIP detection (>= 3 orders)

**Total:** 10 days, 40M VND (vs original 5 days, 20M)

---

## Questions for Client (CRITICAL - BLOCKING)

### 1. API Documentation
- [ ] Nobita API base URL?
- [ ] Authentication: API Key? Bearer Token? OAuth?
- [ ] Rate limits?
- [ ] Có API docs không? Link?

### 2. Check Customer Endpoint
```
POST /api/customers/check
Request: {"phone": "0901234567", "facebook_psid": "123"}
Response: {
  "is_existing": true,
  "is_vip": false,
  "total_orders": 5,
  "risk_score": 0.2,
  "last_order_date": "2026-03-20"
}
```
- [ ] Endpoint URL đúng không?
- [ ] Request/response format đúng không?

### 3. Risk Scoring
- [ ] Risk score range: 0.0-1.0?
- [ ] Threshold gắn "Cảnh báo đỏ": >= 0.7?
- [ ] Tag name: "HIGH_RISK"? "NEEDS_CONFIRMATION"?

### 4. Create Draft Order Endpoint
```
POST /api/orders/draft
Request: {
  "customer": {"name": "...", "phone": "...", "address": "..."},
  "products": [{"code": "KCN", "quantity": 1, "price": 350000}],
  "gifts": [{"code": "GIFT01"}],
  "total": 350000,
  "shipping_fee": 0,
  "notes": "Khách VIP"
}
Response: {"draft_order_id": "DRAFT-001234", "status": "pending_review"}
```
- [ ] Endpoint URL đúng không?
- [ ] Required fields đầy đủ chưa?

---

## Implementation Plan

### Day 1-2: API Client (8h)
- Create `NobitatApiClient` service
- Implement authentication
- Implement `CheckCustomerAsync`
- Implement `CreateDraftOrderAsync`
- Error handling & retry logic

**Files:**
- `Services/Nobita/NobitatApiClient.cs` (new)
- `Services/Nobita/Models/CustomerCheckResponse.cs` (new)
- `Services/Nobita/Models/DraftOrderRequest.cs` (new)

**Cost:** 6M VND

### Day 3: Integration (6h)
- Integrate vào Quick Reply flow
- Check customer trước khi tạo đơn
- Tag đơn hàng rủi ro cao
- Adjust tone cho khách VIP

**Files:**
- `Services/QuickReplyHandler.cs` (update)
- `StateMachine/Handlers/CollectingInfoHandler.cs` (update)

**Cost:** 5M VND

### Day 4-5: Testing (10h)
- Unit tests với mock API
- Integration tests với Nobita sandbox
- Error scenarios
- Performance testing

**Files:**
- `tests/UnitTests/Services/NobitatApiClientTests.cs` (new)
- `tests/IntegrationTests/NobitatIntegrationTests.cs` (new)

**Cost:** 9M VND

---

## Success Criteria

- ✅ Successfully check customer status
- ✅ Create draft order in Nobita
- ✅ Tag high-risk orders
- ✅ Adjust tone for VIP customers
- ✅ Handle API errors gracefully
- ✅ Response time < 3s (including API calls)

---

## Risks

| Risk | Mitigation |
|------|------------|
| API docs không có | Mock API để develop, request docs ASAP |
| API slow (>2s) | Implement caching, async processing |
| API rate limits | Implement rate limiting, queue requests |

---

## Next Phase

Phase 3: Simplified State Machine
