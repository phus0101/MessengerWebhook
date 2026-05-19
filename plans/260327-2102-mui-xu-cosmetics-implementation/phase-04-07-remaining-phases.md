# Phase 4-7: Remaining Phases - Technical Specifications

---

## Phase 4: Human Handoff System

**Duration:** 4 days | **Cost:** 16M VND

### Overview
- Email notification khi bot không xử lý được
- Pause bot cho đến khi nhân viên hoàn thành
- Resume bot sau khi nhân viên click "Hoàn thành"

### Questions (CRITICAL)
- [ ] Email gửi cho ai? `support@muixu.com`?
- [ ] Email template format?
- [ ] Resume API endpoint?
- [ ] Timeout: 30 phút? 1 giờ?

### Implementation
- Day 1-2: Email service (SMTP integration)
- Day 3: Pause/resume logic
- Day 4: Testing

---

## Phase 5: Multi-Tenant Architecture

**Duration:** 5 days | **Cost:** 20M VND

### Overview
- Support nhiều chi nhánh/page
- Mỗi chi nhánh có data riêng
- Mỗi chi nhánh có 1 người quản lý

### Questions (CRITICAL)
- [ ] 1 webhook chung hay nhiều webhook riêng?
- [ ] Phân biệt bằng Page ID?
- [ ] Mỗi chi nhánh có product/stock riêng không?

### Implementation
- Day 1-2: Multi-tenant database schema
- Day 3: Tenant resolution logic
- Day 4: Admin panel (basic)
- Day 5: Testing

---

## Phase 6: Livestream Auto-Reply

**Duration:** 4 days | **Cost:** 16M VND

### Overview
- Tự động nhắn tin cho khách khi comment trên livestream
- Ẩn comment sau khi nhắn tin

### Questions (CRITICAL)
- [ ] Facebook API có support livestream comments không?
- [ ] Webhook event nào?
- [ ] Ẩn comment: API nào?

### Implementation
- Day 1-2: Livestream webhook handler
- Day 3: Auto-reply logic
- Day 4: Testing

---

## Phase 7: Testing & Production

**Duration:** 7 days | **Cost:** 28M VND

### Overview
- Load testing
- Security audit
- Production deployment
- Staff training
- Monitoring setup

### Implementation
- Day 1-2: Load testing (100 concurrent users)
- Day 3: Security audit (OWASP top 10)
- Day 4-5: Production deployment
- Day 6: Staff training
- Day 7: Monitoring & alerting

---

## Total Summary

| Phase | Duration | Cost | Status |
|-------|----------|------|--------|
| 1. Quick Reply | 3 days | 12M | ❌ Not Started |
| 2. Nobita API | 5 days | 20M | ❌ Not Started |
| 3. State Machine | 7 days | 28M | ⚠️ Needs Refactor |
| 4. Human Handoff | 4 days | 16M | ❌ Not Started |
| 5. Multi-Tenant | 5 days | 20M | ❌ Not Started |
| 6. Livestream | 4 days | 16M | ❌ Not Started |
| 7. Production | 7 days | 28M | ❌ Not Started |
| **TOTAL** | **35 days** | **140M** | **7% Complete** |

---

## Critical Path

```
Week 1-2: Phase 1-2 (Foundation)
Week 3-4: Phase 3-4 (Core Features)
Week 5-6: Phase 5-6 (Advanced Features)
Week 7-8: Phase 7 (Production)
```

**Target Launch:** 2026-05-30
