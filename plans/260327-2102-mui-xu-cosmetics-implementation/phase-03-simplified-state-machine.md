# Phase 3: Simplified State Machine - Technical Specification

**Duration:** 7 days | **Cost:** 28M VND | **Status:** ⚠️ Needs Refactor

---

## Overview

Refactor state machine từ 15 states phức tạp → 6 states đơn giản:
- **Idle** → **QuickReply** → **Consulting** → **CollectingInfo** → **DraftOrder** → **Complete**

---

## Current Status

### ✅ Có sẵn (Can Reuse)
- State machine infrastructure
- Session management
- BaseStateHandler pattern
- Database persistence

### ❌ Cần Refactor
- 15 states → 6 states
- Remove: SkinConsultation, BrowsingProducts, ProductDetail, VariantSelection, CartReview, etc.
- Focus: Quick Reply → Collect Info → Draft Order

---

## New State Flow

```
Idle (waiting for first message)
  ↓
QuickReply (handle button click, show product + gift)
  ↓
Consulting (answer FAQs, "ép chốt")
  ↓
CollectingInfo (collect phone + address)
  ↓
DraftOrder (create draft in Nobita)
  ↓
Complete (confirm order created)
```

---

## Implementation Plan

### Day 1-2: Remove Old States (8h)
- Delete unused state handlers
- Update ConversationState enum
- Clean up database

**Cost:** 6M VND

### Day 3-4: Implement New States (10h)
- QuickReplyStateHandler
- ConsultingStateHandler (with "ép chốt" logic)
- CollectingInfoStateHandler
- DraftOrderStateHandler

**Cost:** 10M VND

### Day 5-6: System Prompt (8h)
- Create "ép chốt" system prompt
- Implement tone adjustment (VIP vs normal)
- Test với Gemini AI

**Cost:** 7M VND

### Day 7: Testing (6h)
- Update all tests
- End-to-end flow testing

**Cost:** 5M VND

---

## "Ép Chốt" System Prompt

```
Bạn là nhân viên tư vấn của Múi Xù Cosmetics.

QUY TẮC TỐI THƯỢNG:
- Dù đang trả lời câu hỏi gì, LUÔN LUÔN kết thúc bằng việc xin SĐT + Địa chỉ
- Không bao giờ để đoạn chat kết thúc lửng lơ
- Tự nhiên, ngọt ngào, không khô khan

KHÔNG ĐƯỢC:
- Tự ý hứa miễn phí ship ngoài chính sách
- Tự ý tặng thêm quà ngoài chương trình
- Tự ý giảm giá

VÍ DỤ:
User: "Sản phẩm này có tốt không?"
Bot: "Dạ sản phẩm này rất tốt cho da [loại da], nhiều chị đã dùng và feedback tích cực ạ.
Chị ơi cho em xin số điện thoại và địa chỉ em lên đơn luôn nha 💕"
```

---

## Success Criteria

- ✅ 6 states hoạt động trơn tru
- ✅ "Ép chốt" logic work
- ✅ Tone adjustment cho VIP
- ✅ 70% conversion rate (collect phone + address)
- ✅ All tests pass

---

## Next Phase

Phase 4: Human Handoff System
