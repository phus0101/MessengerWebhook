---
title: "Salesbot Gap Fixes - Phase 1 & 2"
description: "Fix critical gaps in salesbot implementation based on customer requirements"
status: pending
priority: P0
effort: 48h
branch: master
tags: [salesbot, email, prompt, livestream, bot-lock]
created: 2026-03-31
blockedBy: []
blocks: []
---

# Salesbot Gap Fixes Implementation Plan

**Duration:** 1-2 tuần (48 hours total)
**Cost:** 36M VND (12M Phase 1 + 24M Phase 2)
**Status:** 🔄 In Progress (Phase 1 ✅ Complete, Phase 2 75% Complete)
**Priority:** CRITICAL

---

## Overview

Fix critical gaps identified in requirements analysis to meet customer expectations:
- Email notification system với HTML templates và action buttons
- Risk message không để lộ đánh giá khách hàng
- System prompt aggressive hơn về "ép chốt"
- Livestream comment automation
- Bot lock mechanism hoàn chỉnh

**Gap Analysis Report:** `plans/reports/code-reviewer-260331-0915-salesbot-requirements-gap-analysis.md`

---

## Implementation Phases

### ✅ Phase 1: Critical Fixes (1-2 ngày, 16h)
**File**: [phase-01-critical-fixes.md](./phase-01-critical-fixes.md)
**Duration**: 1-2 days
**Cost**: 12M VND
**Status**: ✅ Completed
**Priority**: P0 - CRITICAL

1. Email notification với SMTP và HTML templates
2. Fix risk message để không để lộ đánh giá
3. Enhance system prompt với instruction mạnh hơn

---

### ✅ Phase 2: Livestream & Bot Lock (3-5 ngày, 32h)
**File**: [phase-02-livestream-bot-lock.md](./phase-02-livestream-bot-lock.md)
**Duration**: 3-5 days
**Cost**: 24M VND
**Status**: ✅ Completed
**Priority**: P1 - HIGH
**Dependencies**: Phase 1

4. Implement livestream automation
5. Complete bot lock mechanism với unlock endpoint

---

## Success Criteria

**Phase 1:**
- [ ] Email gửi thành công với HTML template
- [ ] Button "Complete Case" trong email hoạt động
- [ ] Risk message không còn để lộ đánh giá khách hàng
- [ ] System prompt có instruction rõ ràng về "ép chốt"
- [ ] Bot luôn kết thúc response bằng CTA

**Phase 2:**
- [ ] Webhook nhận được comment từ livestream
- [ ] Bot tự động nhắn tin cho người comment
- [ ] Comment tự động ẩn sau khi nhắn tin
- [ ] Bot lock tự động unlock sau timeout
- [ ] Dashboard hiển thị danh sách bot locks
- [ ] Manual unlock từ dashboard hoạt động

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| SMTP credentials invalid | HIGH | MEDIUM | Test với nhiều providers, document setup rõ ràng |
| Email bị spam filter | MEDIUM | HIGH | Dùng SMTP uy tín (SendGrid/AWS SES), config SPF/DKIM |
| Facebook API rate limits | HIGH | MEDIUM | Implement queue, respect rate limits |
| Prompt quá aggressive | MEDIUM | MEDIUM | A/B test, monitor feedback, dễ revert |
| Livestream webhook permission denied | HIGH | MEDIUM | Document permissions cần thiết, test kỹ |

---

## Dependencies

**External:**
- SMTP credentials (Gmail App Password / SendGrid API key / AWS SES)
- Facebook page permissions: `pages_manage_engagement`, `pages_read_engagement`
- PostgreSQL database (đã có)

**Internal:**
- Phase 2 blocked by Phase 1 (cần email system để notify)
- Không conflict với existing plans

---

## Timeline

```
Week 1 (Days 1-2): Phase 1 - Critical Fixes
  Day 1: Email notification + Risk message fix
  Day 2: System prompt enhancement + Testing

Week 2 (Days 3-7): Phase 2 - Livestream & Bot Lock
  Day 3-4: Facebook Graph API integration
  Day 5: Comment processing workflow
  Day 6: Bot lock dashboard enhancement
  Day 7: Testing & integration
```

**Target Completion:** 2026-04-14 (2 weeks from now)

---

## Next Steps

1. Review plan với user
2. Obtain SMTP credentials
3. Request Facebook page permissions
4. Begin Phase 1 implementation
5. Run `/ck:cook` to start implementation

---

## Related Documents

- Gap Analysis: `plans/reports/code-reviewer-260331-0915-salesbot-requirements-gap-analysis.md`
- Customer Requirements: Trong gap analysis report
- System Architecture: `docs/system-architecture.md`
- Code Standards: `docs/code-standards.md`
