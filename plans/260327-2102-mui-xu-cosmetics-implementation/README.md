# Múi Xù Cosmetics Chatbot - Implementation Plan

**Created:** 2026-03-27
**Target Launch:** 2026-05-30 (8 weeks)
**Total Cost:** 140M VND (development) + 42M VND/year (infrastructure)

---

## 📁 Files Structure

```
plans/260327-2102-mui-xu-cosmetics-implementation/
├── README.md (this file)
├── project-plan.md (tổng quan dự án, timeline, cost) [UPDATED 2026-03-27]
├── phase-01-quick-reply-handler.md (spec chi tiết Phase 1)
├── phase-02-nobita-api-integration.md (spec chi tiết Phase 2) [UPDATED 2026-03-27]
├── phase-03-simplified-state-machine.md (spec chi tiết Phase 3)
└── phase-04-07-remaining-phases.md (tóm tắt Phase 4-7)

plans/reports/
├── requirements-260324-1601-mui-xu-cosmetics-clarification.md (câu hỏi cần làm rõ)
└── nobita-api-260327-2113-analysis.md (phân tích Nobita API) [NEW]
```

---

## ⚠️ CRITICAL UPDATE (2026-03-27)

**Nobita API analyzed.** Phát hiện gap nghiêm trọng:
- ❌ Không có API check customer (khách cũ/VIP/risk)
- ❌ Không có API draft order (đơn nháp)
- ❌ Chỉ có API tạo đơn thực (`POST /orders`)

**Impact:**
- Timeline: 35 days → **39 days** (+4 days)
- Cost: 140M → **156M VND** (+16M)
- Phase 2 split: 2A (Nobita basic) + 2B (local draft) + 2C (local tracking)

**Chi tiết:** `plans/reports/nobita-api-260327-2113-analysis.md`

---

## 🎯 Quick Start

### 1. ⚠️ READ THIS FIRST - Nobita API Analysis
**File:** `../reports/nobita-api-260327-2113-analysis.md`

**CRITICAL FINDINGS:**
- Nobita API thiếu customer check, draft order, risk scoring
- Phải implement local draft order system trong chatbot
- Timeline +4 days, cost +16M VND

### 2. Đọc Project Plan
**File:** `project-plan.md`

Tổng quan về:
- Timeline: 39 days (9 weeks) [UPDATED]
- Cost breakdown: 156M VND [UPDATED]
- Current status: 7% complete
- Risk assessment
- Success criteria

### 2. Làm rõ Requirements (CRITICAL)
**File:** `../reports/requirements-260324-1601-mui-xu-cosmetics-clarification.md`

**10 sections câu hỏi cần trả lời:**
1. Nobita API Documentation (BLOCKING)
2. Facebook Quick Reply Format (BLOCKING)
3. Quà Tặng Logic (BLOCKING)
4. Freeship Logic
5. Human Handoff Mechanism (BLOCKING)
6. Multi-Tenant Architecture (BLOCKING)
7. Đơn Nháp Review
8. Tone & Conversation Style
9. Data Sources (Product, FAQs, Policies)
10. Technical Constraints

**⚠️ CRITICAL:** Phases 1-2 không thể bắt đầu cho đến khi có câu trả lời cho các câu hỏi BLOCKING.

### 3. Review Phase Specs

#### Phase 1: Quick Reply Handler (3 days, 12M)
**File:** `phase-01-quick-reply-handler.md`

**Status:** ❌ Not Started
**Blocking Questions:**
- Button payload format?
- Product mapping rules?
- Gift selection logic?
- Freeship threshold?

**Deliverables:**
- Quick Reply/Postback handler
- Product mapping service
- Gift selection service
- Freeship calculation

#### Phase 2: Nobita API Integration (9 days, 36M) [UPDATED]
**Files:**
- `phase-02-nobita-api-integration.md`
- `../reports/nobita-api-260327-2113-analysis.md`

**Status:** ❌ Not Started
**Split into 3 sub-phases:**
- 2A: Basic Nobita (products, orders) - 3 days, 12M
- 2B: Local Draft Order System - 4 days, 16M
- 2C: Local Customer Tracking - 2 days, 8M

**Blocking Questions:**
- OK với local draft order system?
- Nhân viên review đơn nháp ở đâu?
- OK với local risk scoring?

**Deliverables:**
- Nobita API client (products, orders)
- Local draft order database
- Local customer tracking
- Risk scoring logic

#### Phase 3: Simplified State Machine (7 days, 28M)
**File:** `phase-03-simplified-state-machine.md`

**Status:** ⚠️ Needs Refactor
**Current:** 15 states (too complex)
**Target:** 6 states (sales-focused)

**New Flow:**
```
Idle → QuickReply → Consulting → CollectingInfo → DraftOrder → Complete
```

**Deliverables:**
- Refactored state machine
- "Ép chốt" system prompt
- VIP tone adjustment
- Updated tests

#### Phase 4-7: Advanced Features (20 days, 80M)
**File:** `phase-04-07-remaining-phases.md`

**Phase 4:** Human Handoff (4 days, 16M)
**Phase 5:** Multi-Tenant (5 days, 20M)
**Phase 6:** Livestream Auto-Reply (4 days, 16M)
**Phase 7:** Testing & Production (7 days, 28M)

---

## 📊 Project Status

### Current MVP Status

| Component | Status | Notes |
|-----------|--------|-------|
| Webhook Integration | ✅ Complete | Can reuse |
| Database (PostgreSQL) | ✅ Complete | Can reuse |
| Gemini AI | ✅ Complete | Can reuse |
| Session Management | ✅ Complete | Can reuse |
| Vietnamese Localization | ✅ Complete | Can reuse |
| State Machine | ⚠️ Needs Refactor | Too complex, wrong focus |
| Quick Reply Handler | ❌ Missing | Must build |
| Nobita Integration | ❌ Missing | Must build |
| Human Handoff | ❌ Missing | Must build |
| Multi-Tenant | ❌ Missing | Must build |

**Overall Progress:** 7% Complete

---

## 🚀 Implementation Roadmap

### Week 0: Requirements Clarification (CURRENT)
- [ ] Send clarification questions to client
- [ ] Get Nobita API documentation
- [ ] Confirm Quick Reply format
- [ ] Define gift selection logic
- [ ] Get product/gift data files

### Week 1-2: Foundation (Phase 1, 2A-2C)
- [ ] Implement Quick Reply handler
- [ ] Integrate Nobita API (basic)
- [ ] Build local draft order system
- [ ] Build local customer tracking
- [ ] Test end-to-end flow

### Week 3-4: Core Features (Phase 3-4)
- [ ] Refactor state machine
- [ ] Implement "ép chốt" logic
- [ ] Build human handoff system

### Week 5-6: Advanced Features (Phase 5-6)
- [ ] Multi-tenant architecture
- [ ] Livestream auto-reply

### Week 7-8: Production (Phase 7)
- [ ] Load testing
- [ ] Security audit
- [ ] Deployment
- [ ] Staff training

---

## 💰 Cost Summary

### Development (One-time)
| Phase | Duration | Cost |
|-------|----------|------|
| Phase 1: Quick Reply | 3 days | 12M |
| Phase 2A: Nobita API (Basic) | 3 days | 12M |
| Phase 2B: Local Draft Order | 4 days | 16M |
| Phase 2C: Local Customer Tracking | 2 days | 8M |
| Phase 3: State Machine | 7 days | 28M |
| Phase 4: Human Handoff | 4 days | 16M |
| Phase 5: Multi-Tenant | 5 days | 20M |
| Phase 6: Livestream | 4 days | 16M |
| Phase 7: Production | 7 days | 28M |
| **Total** | **39 days** | **156M** |

### Infrastructure (Monthly)
- VPS/Cloud: 2M
- Database: 1M
- Gemini API: 500k
- **Total:** 3.5M/month

### Maintenance (Monthly)
- Bug fixes: 5M
- Support: 3M
- **Total:** 8M/month

**First Year Total:** 156M + 42M + 96M = **294M VND**

---

## ⚠️ Critical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Nobita API gaps (CONFIRMED) | HIGH | Build local draft order + tracking |
| Requirements unclear | HIGH | Clarify before Phase 1 |
| Facebook API changes | MEDIUM | Use official SDK |
| Multi-tenant complexity | MEDIUM | Start single-tenant |

---

## ✅ Success Criteria

1. **Conversion Rate:** 70% chats → collect phone + address
2. **Automation:** 80-90% chats without human
3. **Response Time:** < 3s average
4. **Uptime:** 99% during business hours
5. **Draft Order Accuracy:** 95% correct info

---

## 📞 Next Steps

### Immediate Actions (This Week)

1. **Review Requirements Clarification**
   - File: `../reports/requirements-260324-1601-mui-xu-cosmetics-clarification.md`
   - Send to client for answers

2. **Get Critical Data**
   - Nobita API documentation
   - Product/gift data files (Excel/CSV)
   - Sample conversations for tone
   - FAQs & policies documents

3. **Approve Project Plan**
   - Review timeline
   - Confirm budget
   - Sign off on approach

### After Requirements Clear

4. **Kickoff Phase 1**
   - Setup development environment
   - Create project board
   - Begin Quick Reply implementation

---

## 📝 Notes

### Key Differences from Current MVP

**Current MVP:**
- 15 states (complex)
- Focus: Skin consultation + AI recommendations
- Vector search for products
- Detailed product browsing

**New Requirements:**
- 6 states (simple)
- Focus: Sales conversion + "ép chốt"
- Quick Reply → Collect Info → Draft Order
- 70% customers already know what to buy

**Refactor Strategy:**
- Keep: Infrastructure, database, AI integration
- Simplify: State machine, conversation flow
- Add: Quick Reply, Nobita, Human Handoff, Multi-tenant

### Assumptions

1. Nobita API is available and documented
2. Facebook Page tokens provided
3. Email server available
4. PostgreSQL database provisioned
5. Staff available for testing

---

## 📚 Additional Resources

- [Facebook Messenger Platform Docs](https://developers.facebook.com/docs/messenger-platform/)
- [Quick Reply Documentation](https://developers.facebook.com/docs/messenger-platform/send-messages/quick-replies/)
- [Postback Documentation](https://developers.facebook.com/docs/messenger-platform/reference/webhook-events/messaging_postbacks)
- [Gemini API Docs](https://ai.google.dev/gemini-api/docs)

---

## 🤝 Contact

**Questions?** Review the requirements clarification document first, then reach out to project team.

**Ready to start?** Ensure all BLOCKING questions are answered before beginning Phase 1.
