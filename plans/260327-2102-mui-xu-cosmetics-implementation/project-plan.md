# Múi Xù Cosmetics Chatbot - Project Plan

**Date:** 2026-03-27
**Client:** Múi Xù Cosmetics
**Project Type:** Facebook Messenger Chatbot for Sales Conversion
**Target:** Replace 80-90% of fanpage staff workload

---

## Executive Summary

Refactor existing MVP to match real business requirements:
- **Current MVP:** Complex skin consultation bot (15 states, vector search, AI recommendations)
- **Actual Need:** Simple sales conversion bot (Quick Reply → Collect Info → Draft Order)
- **Key Change:** Simplify from consultation-focused to sales-focused

---

## Project Phases

| Phase | Description | Duration | Cost (VND) | Status |
|-------|-------------|----------|------------|--------|
| 1 | Quick Reply Handler | 3 days | 12M | Not Started |
| 2A | Nobita API (Basic) | 3 days | 12M | Not Started |
| 2B | Draft Order + Email | 5 days | 20M | Not Started |
| 2C | Customer Tracking | 2 days | 8M | Not Started |
| 3 | Simplified State Machine | 7 days | 28M | Partial |
| 4 | Human Handoff System | 4 days | 16M | Not Started |
| 5 | Multi-Tenant Architecture | 5 days | 20M | Not Started |
| 6 | Livestream Auto-Reply | 4 days | 16M | Not Started |
| 7 | Testing & Production | 7 days | 28M | Not Started |
| **Total** | **MVP → Production** | **40 days** | **160M** | **7%** |

**Updated:** 2026-03-27 21:26 - Client confirmed draft order workflow (+1 day, +4M)

---

## Timeline

```
Week 1-2: Phase 1, 2A-2C (Quick Reply + Nobita)
Week 3-4: Phase 3-4 (State Machine + Handoff)
Week 5-6: Phase 5-6 (Multi-tenant + Livestream)
Week 7-8: Phase 7 (Testing + Production)
```

**Target Launch:** 2026-06-05 (9 weeks from now)
**Updated:** 2026-03-27 (+4 days due to Nobita API gaps)

---

## Current MVP Status

### ✅ Completed (Can Reuse)
- Facebook Messenger webhook integration
- PostgreSQL database with EF Core
- Gemini AI integration
- Session management
- Vietnamese localization
- Unit/integration test infrastructure

### ⚠️ Needs Refactor
- State machine (too complex, wrong focus)
- Product/variant entities (cosmetics-specific)
- Conversation flow (consultation → sales)

### ❌ Missing (Must Build)
- Quick Reply button handler
- Nobita API client
- Risk scoring & tagging
- Human handoff (email notification)
- Multi-tenant support
- Livestream comment auto-reply

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| Nobita API docs missing | HIGH | Request API docs upfront, mock if needed |
| Facebook API changes | MEDIUM | Use official SDK, monitor deprecations |
| Multi-tenant complexity | MEDIUM | Start with single tenant, add later |
| Human handoff timeout | LOW | Set reasonable timeout (30min) |

---

## Success Criteria

1. **Conversion Rate:** 70% of chats → collect phone + address
2. **Automation:** 80-90% chats handled without human
3. **Response Time:** < 3s average
4. **Uptime:** 99% during business hours
5. **Draft Order Accuracy:** 95% correct info

---

## Cost Breakdown

### Development (156M VND)
- Backend development: 92M (+12M for local draft/tracking)
- Integration (Nobita, Facebook): 30M
- Testing & QA: 24M (+4M)
- Documentation: 10M

**Updated:** 2026-03-27 (+16M due to local draft order system)

### Infrastructure (Monthly)
- VPS/Cloud hosting: 2M
- Database (PostgreSQL): 1M
- Gemini API: 500k
- Facebook API: Free
- **Total/month:** 3.5M

### Maintenance (Monthly)
- Bug fixes & updates: 5M
- Monitoring & support: 3M
- **Total/month:** 8M

**First Year Total:** 156M (dev) + 42M (infra) + 96M (maintenance) = **294M VND**

**Updated:** 2026-03-27 (+16M dev cost)

---

## Next Steps

1. **Clarify Requirements** (Week 0)
   - Get Nobita API documentation
   - Confirm Quick Reply payload format
   - Define gift selection logic
   - Get sample conversations for tone

2. **Kickoff** (Week 1)
   - Review & approve specs
   - Setup development environment
   - Create project board

3. **Development** (Week 1-7)
   - Follow phase-by-phase implementation
   - Weekly demos to stakeholders
   - Continuous testing

4. **Launch** (Week 8)
   - Production deployment
   - Staff training
   - Monitoring setup

---

## Deliverables

- ✅ Functional chatbot (Quick Reply → Draft Order)
- ✅ Nobita integration (customer check, draft order)
- ✅ Human handoff system (email notification)
- ✅ Multi-tenant support (multiple pages)
- ✅ Livestream auto-reply
- ✅ Admin dashboard (draft order review)
- ✅ Documentation (API, deployment, maintenance)
- ✅ Training materials (for staff)

---

## Assumptions

1. Nobita API is available and documented
2. Facebook Page access tokens provided
3. Email server (SMTP) available for notifications
4. PostgreSQL database provisioned
5. Staff available for testing & feedback

---

## Dependencies

- Nobita API documentation (CRITICAL)
- Facebook Page tokens (CRITICAL)
- Sample product/gift data (HIGH)
- Sample FAQs & policies (HIGH)
- Email server credentials (MEDIUM)

---

## Contact

**Project Manager:** [TBD]
**Tech Lead:** [TBD]
**Client Contact:** Múi Xù Cosmetics
**Timeline:** 2026-03-27 → 2026-05-30
