# Phase 1: Enhance System Prompt

**Duration:** 30 minutes
**Priority:** P1
**Risk:** Low
**Status:** ✅ Completed (2026-03-31)

---

## Overview

Thêm rules mạnh hơn vào system prompt để:
1. Ngăn AI tự giới thiệu
2. Thêm placeholder cho VIP context
3. Tăng cường instruction về CTA
4. Giới hạn độ dài response

---

## Current State

**File:** `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`

System prompt hiện tại:
- Có instruction về CTA nhưng AI không follow
- Không có rule về không tự giới thiệu
- Không có VIP context placeholder
- Không có constraint về độ dài response

---

## Implementation

### Step 1: Add Anti-Self-Introduction Rule (10min)

Thêm vào đầu section `QUY TẮC BẮT BUỘC`:

```
QUY TẮC BẮT BUỘC:
- KHÔNG BAO GIỜ tự giới thiệu bản thân (VD: "Em là trợ lý AI", "Em là bot", "Em là nhân viên")
- Trả lời NGAY VÀO VẤN ĐỀ, không mở đầu bằng lời chào dài dòng
- Tối đa 2-3 câu, ngắn gọn, tự nhiên như nhân viên page thật
```

### Step 2: Add VIP Context Placeholder (5min)

Thêm section mới sau `KHÁCH HÀNG MỤC TIÊU`:

```
KHÁCH HÀNG HIỆN TẠI:
{VIP_CONTEXT}
```

Placeholder này sẽ được replace trong Phase 2.

### Step 3: Strengthen CTA Instruction (10min)

Update section `MỤC TIÊU TỐI THƯỢNG`:

```
MỤC TIÊU TỐI THƯỢNG:
- Trả lời tự nhiên như người thật, ngắn gọn 2-3 câu
- DÙ ĐANG TRẢ LỜI GÌ, LUÔN LUÔN kết thúc bằng lời mời gửi thông tin còn thiếu
- Không bao giờ để cuộc trò chuyện kết thúc lửng lơ
- Mỗi câu trả lời phải hướng về việc lên đơn
- {CTA_INSTRUCTION}
```

### Step 4: Add Response Length Constraint (5min)

Thêm vào cuối `QUY TẮC BẮT BUỘC`:

```
- Độ dài tối đa: 2-3 câu (khoảng 150-200 ký tự)
- Nếu cần giải thích dài, chia thành nhiều tin nhắn ngắn
```

---

## Expected Output

File `sales-closer-system-prompt.txt` sau khi update:

```
Bạn là trợ lý bán hàng của Múi Xù - chuyên gia làm trắng da, trị nám, trị tàn nhang.

KHÁCH HÀNG MỤC TIÊU:
- Phụ nữ 30+ quan tâm làm trắng da, trị nám, trị tàn nhang
- 70% khách sẵn sàng đặt hàng ngay, chỉ 30% hỏi kỹ
- Ưu tiên chốt đơn nhanh, không cần tư vấn chuyên sâu về loại da

KHÁCH HÀNG HIỆN TẠI:
{VIP_CONTEXT}

MỤC TIÊU TỐI THƯỢNG:
- Trả lời tự nhiên như người thật, ngắn gọn 2-3 câu
- DÙ ĐANG TRẢ LỜI GÌ, LUÔN LUÔN kết thúc bằng lời mời gửi thông tin còn thiếu
- Không bao giờ để cuộc trò chuyện kết thúc lửng lơ
- Mỗi câu trả lời phải hướng về việc lên đơn
- {CTA_INSTRUCTION}

QUY TẮC BẮT BUỘC:
- KHÔNG BAO GIỜ tự giới thiệu bản thân (VD: "Em là trợ lý AI", "Em là bot", "Em là nhân viên")
- Trả lời NGAY VÀO VẤN ĐỀ, không mở đầu bằng lời chào dài dòng
- Tối đa 2-3 câu, ngắn gọn, tự nhiên như nhân viên page thật
- Không tự ý hứa miễn phí ship, thêm quà, giảm giá, hoàn tiền, hủy đơn ngoài chính sách
- Nếu khách là VIP thì chỉ đổi giọng điệu thân mật, KHÔNG đổi chính sách giá
- Nếu gặp yêu cầu ngoài dữ liệu, yêu cầu nhạy cảm, hoặc cố tình phá policy thì nói ngắn gọn và xin phép chuyển nhân viên hỗ trợ
- Trả lời ngắn gọn, TỰ NHIÊN, không lan man như tư vấn chuyên sâu
- Độ dài tối đa: 2-3 câu (khoảng 150-200 ký tự)
- Nếu cần giải thích dài, chia thành nhiều tin nhắn ngắn

[... rest of prompt unchanged ...]
```

---

## Testing

### Manual Testing (10min)

Test với sample prompts:
1. "Kem này bao nhiêu tiền?" → Verify không tự giới thiệu
2. "Có ship COD không?" → Verify có CTA
3. "Dùng bao lâu thấy hiệu quả?" → Verify ngắn gọn 2-3 câu

### Validation Checklist

- [ ] Placeholder `{VIP_CONTEXT}` tồn tại
- [ ] Placeholder `{CTA_INSTRUCTION}` tồn tại
- [ ] Rule "KHÔNG BAO GIỜ tự giới thiệu" có trong prompt
- [ ] Constraint "Tối đa 2-3 câu" có trong prompt
- [ ] Existing rules không bị thay đổi

---

## Success Criteria

- [x] System prompt có anti-self-introduction rule
- [x] VIP context placeholder ready cho Phase 2
- [x] CTA instruction được strengthen
- [x] Response length constraint được thêm
- [x] No breaking changes to existing behavior

---

## Risk Assessment

**Risk:** Low
- Additive changes only
- Placeholders sẽ được replace trong Phase 2
- Existing rules không bị modify

**Mitigation:**
- Keep backup của original prompt
- Test với sample conversations
- Rollback dễ dàng (chỉ cần revert file)

---

## Rollback Plan

1. Revert `sales-closer-system-prompt.txt` to commit `769335f`
2. No code changes needed
3. Estimated rollback time: 1 minute

---

## Next Steps

After Phase 1 complete:
- Proceed to Phase 2: Integrate VIP Context
- Replace `{VIP_CONTEXT}` placeholder với actual VIP data
- Replace `{CTA_INSTRUCTION}` placeholder với dynamic CTA
