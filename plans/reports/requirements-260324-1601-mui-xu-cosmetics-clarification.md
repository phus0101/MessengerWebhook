# Requirements Clarification - Múi Xù Cosmetics Chatbot

**Date:** 2026-03-24
**Status:** Pending Customer Response
**Purpose:** Làm rõ yêu cầu trước khi refactor MVP

---

## 1. Nobita API Integration

### 1.1 API Documentation
- [ ] Nobita có API documentation không? Link?
- [ ] Authentication method: API Key? OAuth? Bearer Token?
- [ ] Base URL của API?
- [ ] Rate limits?

### 1.2 Check Khách Cũ / VIP
**Endpoint:**
- [ ] URL: `POST /api/customers/check` ?
- [ ] Request body format?
```json
{
  "phone": "0901234567",
  "facebook_psid": "123456789"
}
```

**Response format:**
- [ ] Trả về gì?
```json
{
  "is_existing": true,
  "is_vip": false,
  "total_orders": 5,
  "risk_score": 0.2,
  "last_order_date": "2026-03-20"
}
```

### 1.3 Risk Scoring (Chống Bom Hàng)
- [ ] Risk score range: 0.0 - 1.0?
- [ ] Threshold để gắn "Cảnh báo đỏ": >= 0.7?
- [ ] Thuật toán tính risk score: dựa vào gì?
  - Tỷ lệ nhận hàng thành công?
  - Số lần bom hàng?
  - Thời gian từ lần bom gần nhất?
- [ ] Tag/Label cho đơn hàng rủi ro: "HIGH_RISK"? "NEEDS_CONFIRMATION"?

### 1.4 Tạo Đơn Nháp
**Endpoint:**
- [ ] URL: `POST /api/orders/draft` ?
- [ ] Request body format?
```json
{
  "customer": {
    "name": "Nguyễn Văn A",
    "phone": "0901234567",
    "address": "123 Đường ABC, Quận 1, TP.HCM",
    "facebook_psid": "123456789"
  },
  "products": [
    {"code": "KCN", "quantity": 1, "price": 350000}
  ],
  "gifts": [
    {"code": "GIFT01", "quantity": 1}
  ],
  "total": 350000,
  "shipping_fee": 0,
  "notes": "Khách VIP, ưu tiên giao hàng"
}
```

**Response:**
- [ ] Trả về draft order ID?
```json
{
  "draft_order_id": "DRAFT-2026-001234",
  "status": "pending_review"
}
```

---

## 2. Facebook Quick Reply Buttons

### 2.1 Payload Format
Khi khách click button từ Facebook Ads, webhook nhận payload gì?

**Option A: Text-based**
```json
{
  "sender": {"id": "123456789"},
  "message": {
    "text": "Tôi muốn mua Kem Chống Nắng nhé Múi ơi"
  }
}
```

**Option B: Postback-based**
```json
{
  "sender": {"id": "123456789"},
  "postback": {
    "title": "Tôi muốn mua Kem Chống Nắng",
    "payload": "PRODUCT_KCN"
  }
}
```

- [ ] Khách hàng dùng option nào?
- [ ] Nếu dùng postback, mapping giữa payload và product code?

### 2.2 Product Mapping
- [ ] 3 buttons hiện tại:
  - "Tôi muốn mua Kem Chống Nắng" → Product code: `KCN`?
  - "Tôi muốn mua Kem Lụa" → Product code: `KL`?
  - "Tôi muốn mua 2 sản phẩm để được freeship" → Combo code: `COMBO_2`?

---

## 3. Quà Tặng Logic

### 3.1 Chọn Quà
- [ ] 20 mã quà tặng được **chọn tự động** hay **khách chọn**?
- [ ] Nếu tự động, logic chọn quà:
  - Theo sản phẩm? (VD: Mua KCN → tặng Gift A)
  - Theo giá trị đơn? (VD: Đơn >= 500k → tặng Gift B)
  - Theo chương trình KM? (VD: Tháng 3 → tặng Gift C)
  - Random từ pool?

### 3.2 Hiển Thị Quà
Khi bot reply "Sản Phẩm + Quà Tặng", format như thế nào?

**Example:**
```
✨ Dạ em xin phép gửi chị thông tin:

📦 Sản phẩm: Kem Chống Nắng SPF50+ (350.000đ)
🎁 Quà tặng: Sữa rửa mặt mini 50ml

Tổng cộng: 350.000đ (Miễn phí vận chuyển)

Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕
```

- [ ] Format này OK không?
- [ ] Có cần thêm hình ảnh sản phẩm không?

---

## 4. Freeship Logic

### 4.1 Điều Kiện
"Mua 2 sản phẩm để được freeship" nghĩa là:
- [ ] **Option A:** Mua >= 2 items (bất kỳ sản phẩm nào)
- [ ] **Option B:** Giá trị đơn hàng >= X đồng
- [ ] **Option C:** Chọn combo cố định (VD: KCN + KL)

### 4.2 Phí Ship
- [ ] Freeship threshold cố định toàn quốc hay theo khu vực?
- [ ] Nếu không đủ điều kiện freeship, phí ship bao nhiêu?
  - Nội thành: 30k?
  - Ngoại thành: 50k?
  - Tỉnh xa: 70k?

---

## 5. Human Handoff (Chuyển Nhân Viên)

### 5.1 Trigger Conditions
Bot chuyển cho nhân viên khi:
- [ ] Khách hỏi câu ngoài FAQs (AI không trả lời được)
- [ ] Khách yêu cầu hủy đơn / hoàn tiền
- [ ] Khách yêu cầu discount / freeship ngoài chính sách
- [ ] Khách prompt injection (hack bot)
- [ ] Khác: _____________

### 5.2 Email Notification
**Gửi cho ai:**
- [ ] Email cố định: `support@muixu.com`?
- [ ] Hay theo chi nhánh? (VD: HCM → `hcm@muixu.com`, HN → `hn@muixu.com`)

**Email format:**
```
Subject: [Múi Xù Bot] Cần hỗ trợ khách hàng - PSID: 123456789

Khách hàng: Nguyễn Văn A
SĐT: 0901234567
Facebook PSID: 123456789
Lý do: Yêu cầu hủy đơn

Lịch sử chat: [Link to conversation]

[Button: Hoàn thành case]
```

- [ ] Format này OK không?
- [ ] Link to conversation: Nobita có dashboard để xem chat history không?

### 5.3 Resume Bot
- [ ] Button "Hoàn thành case" trong email → call API nào?
  - `POST /api/bot/resume?psid=123456789`?
- [ ] Timeout: Nếu nhân viên không xử lý sau **X phút**, bot tự động resume?
  - 30 phút? 1 giờ? 2 giờ?

---

## 6. Multi-Tenant (Nhiều Chi Nhánh)

### 6.1 Architecture
- [ ] Mỗi chi nhánh có **webhook riêng** hay **dùng chung 1 webhook**?
  - Nếu riêng: Mỗi chi nhánh deploy 1 instance riêng?
  - Nếu chung: Phân biệt chi nhánh bằng Page ID trong webhook payload?

### 6.2 Data Isolation
- [ ] Mỗi chi nhánh có data riêng (sản phẩm, tồn kho, KM) hay dùng chung?
- [ ] Nếu riêng: Cần multi-tenant database schema?
  - Table `products` có column `branch_id`?
  - Table `orders` có column `branch_id`?

### 6.3 Quản Lý
- [ ] Mỗi chi nhánh có 1 người quản lý riêng → họ quản lý ở đâu?
  - Nobita dashboard?
  - Admin panel riêng?

---

## 7. Đơn Nháp Review

### 7.1 Review Process
- [ ] Nhân viên review đơn nháp ở đâu?
  - Nobita dashboard?
  - Admin panel riêng?
  - Email notification?

### 7.2 Actions
Nhân viên có thể làm gì với đơn nháp:
- [ ] Approve → Gửi cho bên giao hàng
- [ ] Edit → Sửa thông tin (SĐT, địa chỉ, sản phẩm)
- [ ] Reject → Hủy đơn (gọi điện xác nhận khách)
- [ ] Tag → Gắn nhãn (VIP, High Risk, Urgent)

---

## 8. Tone & Conversation Style

### 8.1 Sample Conversations
- [ ] Có sample conversations thực tế không? (3-5 đoạn chat)
  - Để tôi học tone "đon đả, ngọt ngào" của brand
  - Để tôi hiểu cách "ép chốt" nhưng không khô khan

### 8.2 "Ép Chốt" Examples
Ví dụ câu "ép chốt" tốt:
```
❌ Khô khan: "Vui lòng cung cấp số điện thoại và địa chỉ."
✅ Tự nhiên: "Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕"
```

- [ ] Có thêm ví dụ nào khác không?

### 8.3 Khách VIP Tone
Khách VIP (mua nhiều lần) → văn phong "đon đả, ngọt ngào":
```
❌ Generic: "Xin chào! Bạn cần gì ạ?"
✅ VIP: "Dạ em chào chị khách quen của Múi Xù! Hôm nay chị cần em tư vấn gì ạ? 🥰"
```

- [ ] Có thêm ví dụ nào khác không?

---

## 9. Data Sources

### 9.1 Sản Phẩm
- [ ] 10 mã sản phẩm chính: Có file Excel/CSV không?
  - Columns: `code`, `name`, `price`, `description`, `image_url`, `stock`?

### 9.2 FAQs
- [ ] FAQs: Có file nào không?
  - Format: Question → Answer pairs?
  - Bao nhiêu câu hỏi? (10? 50? 100?)

### 9.3 Chính Sách
- [ ] Chính sách đổi trả, bảo hành, vận chuyển: Có document không?

### 9.4 Chương Trình KM
- [ ] Chương trình khuyến mãi hiện tại: Có file nào không?
  - VD: "Mua 2 tặng 1", "Giảm 10% cho đơn >= 500k"

---

## 10. Technical Constraints

### 10.1 Response Time
- [ ] Bot phải reply trong bao lâu? (< 3s? < 5s?)

### 10.2 Concurrent Users
- [ ] Dự kiến bao nhiêu khách nhắn tin cùng lúc? (10? 50? 100?)

### 10.3 Uptime
- [ ] Yêu cầu uptime: 99%? 99.9%?

---

## Next Steps

Sau khi có câu trả lời cho các câu hỏi trên, tôi sẽ:

1. **Tạo Technical Specification Document**
2. **Refactor Plan MVP** (Option 1 - Recommended)
   - Đơn giản hóa state machine
   - Focus vào "ép chốt" + Nobita integration
3. **Estimate Timeline** (sau khi có spec rõ ràng)

---

## Questions?

Nếu có câu hỏi nào chưa rõ hoặc cần thêm context, vui lòng note lại đây:

- _____________
- _____________
- _____________
