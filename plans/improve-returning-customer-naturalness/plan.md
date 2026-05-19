---
title: "Improve Returning Customer Naturalness"
description: "Older narrow plan for returning-customer greeting and tone; now superseded by broader sales prompt and product-lock flow plan."
status: pending
priority: P2
created: 2026-04-06
blockedBy: [260413-2107-sales-prompt-and-product-lock-flow]
blocks: []
---

# Plan: Improve Returning Customer Naturalness

## Problem

Khách cũ nhắn "hi sốp" → Bot trả lời như lần đầu gặp: "Dạ em chào chị khách quen của Múi Xù ạ! Múi Xù chuyên các sản phẩm làm trắng da, trị nám, tàn nhang ạ. Chị đang quan tâm sản phẩm nào ạ?"

Nguyên nhân: Bot giới thiệu lại catalog cho khách đã biết, tone quá formal, không bắt chước casual của khách.

## Root Cause Analysis

1. **Thiếu returning customer context**: Only `IsVip` customers get special greeting. Khách cũ (`Returning`) không có instruction nào.
2. **VipInstruction quá dài**: "Sau đó giới thiệu sản phẩm và hỏi nhu cầu" → AI hiểu thành "phải giới thiệu lại sản phẩm".
3. **GreetingStyle không được truyền tải đúng**: Giá trị `"Dạ chào chị ạ! Vui quá được gặp lại chị."` không phải là template mà là instruction — AI tự generate greeting theo ý mình, không dùng greeting string này.
4. **Bot không match tone**: Khách casual → bot formal. Bot không nhận diện được "hi sốp" là mở đầu casual.

## Implementation

### Phase 1: Add returning customer instruction

**Files:** `SalesStateHandlerBase.cs` (BuildVipInstruction → BuildVipInstruction)

- Thêm xử lý cho khách cũ không VIP (tier `Returning`): greeting nhẹ nhàng, không giới thiệu lại catalog
- Sửa VipInstruction: thay "giới thiệu sản phẩm" thành "hỏi thăm nhu cầu"
- Bot KHÔNG cần phải tự greet — instruction chỉ yêu cầu tone, không yêu cầu greet template

**Key changes:**
- Refactor `BuildVipInstruction` → `BuildCustomerInstruction` (bao gồm VIP + returning)
- Thêm `Returning` tier vào `VipProfile.Tier` check

### Phase 2: Improve system prompt tone detection

**Files:** `SalesStateHandlerBase.cs`

Thêm vào prompt system:
- Nhận diện tone của customer message (formal/casual/friendly) và mirror tone
- Cho returning customers: KHÔNG giới thiệu lại page, KHÔNG giới thiệu lại sản phẩm
- Instruction ngắn gọn: "Khách quen — trả lời như bạn cũ, không giới thiệu lại"

### Phase 3: Tests

**Files:** New test file or add to existing SalesStateHandlerBaseTests

- Test: Returning customer gets natural greeting (no catalog intro)
- Test: Tone matching (casual → casual response)
- Test: VIP greeting still works

## Files to Modify

| File | Change |
|------|--------|
| `SalesStateHandlerBase.cs` | BuildVipInstruction → BuildCustomerInstruction, thêm returning + tone matching |
| `CustomerIntelligenceService.cs` | Đảm bảo VipProfile được load đầy đủ (đã có sẵn) |
| Existing tests | Fix constructor signatures if needed |
| New tests | Verify returning customer flows |

## Status: Pending Approval
