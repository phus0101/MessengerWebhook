---
type: naturalness-analysis
date: 2026-04-15
status: completed
---

# Phân tích độ tự nhiên của hội thoại Sales Bot

## Tổng quan

Phân tích này đánh giá độ tự nhiên của conversation flow trong sales bot sau khi implement production-ready patches.

## 1. Remembered Contact Confirmation Flow

### Điểm mạnh ✅

**Xử lý partial contact gracefully:**
```csharp
// SalesStateHandlerBase.cs:1257-1280
if (hasPhone && hasAddress)
    return "Dạ em đang giữ SĐT {phone} và địa chỉ {address} từ lần trước...";
if (hasPhone)
    return "Dạ em đang giữ SĐT {phone} từ lần trước... gửi em thêm địa chỉ...";
if (hasAddress)
    return "Dạ em đang giữ địa chỉ {address} từ lần trước... gửi em thêm số điện thoại...";
```

**Tự nhiên:** Bot phân biệt rõ 3 scenarios (cả 2, chỉ phone, chỉ address) và hỏi đúng thông tin còn thiếu.

**Generic phrase detection:**
```csharp
// SalesStateHandlerBase.cs:1598-1616
IsGenericBuyContinuationWhileAwaitingContactConfirmation()
- Phát hiện: "ok", "oke", "chot nhe", "dat luon", "len don"
- Loại trừ explicit: "dung roi", "van dung", "nhu cu", "thong tin cu"
```

**Tự nhiên:** Bot hiểu được sự khác biệt giữa generic acknowledgment ("ok") vs explicit confirmation ("đúng rồi").

### Điểm cần cải thiện 🔶

**Wording có thể ngắn gọn hơn:**
```
Hiện tại: "Dạ em đang giữ SĐT {phone} từ lần trước của chị ạ. Chị giúp em xác nhận số này còn dùng đúng không, rồi gửi em thêm địa chỉ giao hàng hiện tại để em chốt đơn cho mình nha."

Có thể: "Dạ em có SĐT {phone} từ lần trước. Chị xác nhận số này + gửi thêm địa chỉ giao hàng nha."
```

**Lý do chưa sửa:** Theo YAGNI principle, wording hiện tại đã đủ rõ ràng và không gây lỗi logic. Cải thiện này thuộc "nice-to-have" chứ không phải "must-have" cho production.

## 2. Product Lock Flow

### Điểm mạnh ✅

**Semantic product detection:**
```csharp
// TranscriptGoldenFlowTests.cs:104-106
turn2 = "mặt nạ ngủ này có khuyến mãi gì không em, kem lụa chị chưa cần"
turn2.Should().ContainEquivalentOf("mat na ngu");
turn2.Should().NotContainEquivalentOf("kem lua");
```

**Tự nhiên:** Bot hiểu được câu phức tạp có cả product chính (mặt nạ ngủ) và product bị từ chối (kem lụa), giữ đúng product lock.

**Context stability across turns:**
```csharp
// TranscriptGoldenFlowTests.cs:108-110
turn3 = "có freeship không em"
turn3.Should().ContainEquivalentOf("mat na ngu");
turn3.Should().NotContainEquivalentOf("kem lua");
```

**Tự nhiên:** Câu hỏi về policy không làm mất product context, bot trả lời đúng về mặt nạ ngủ chứ không drift sang kem lụa.

### Điểm cần cải thiện 🔶

**Không có explicit acknowledgment khi user từ chối product:**
```
User: "mặt nạ ngủ này có khuyến mãi gì không em, kem lụa chị chưa cần"
Bot: [trả lời về mặt nạ ngủ]

Có thể tự nhiên hơn:
Bot: "Dạ được ạ, em tư vấn mặt nạ ngủ cho chị nha. [policy info]"
```

**Lý do chưa sửa:** Không phải root cause của bug hiện tại. Improvement này cần thêm intent detection cho "rejection acknowledgment" - nằm ngoài scope của minimal-change patch.

## 3. Order Estimate Reply

### Điểm mạnh ✅

**Structured information:**
```csharp
// SalesStateHandlerBase.cs:1306
return $"Dạ nếu mình chốt {product.Name} thì đơn sẽ có tổng cộng {totalProducts} sản phẩm gồm {unitLabel}{giftLabel} ạ. Tạm tính hàng {merchandiseTotal:N0}đ, phí ship {(shippingFee == 0 ? "miễn phí" : $"{shippingFee:N0}đ")}, tổng tiền hiện tại là {grandTotal:N0}đ ạ.";
```

**Tự nhiên:** 
- Breakdown rõ ràng: số lượng sản phẩm → giá hàng → phí ship → tổng
- Conditional wording: "miễn phí" vs "{amount}đ"
- Tone nhất quán: "Dạ", "ạ", "mình"

### Điểm cần cải thiện 🔶

**Có thể thêm context về gift eligibility:**
```
Hiện tại: "...gồm 1 hũ Mặt Nạ Ngủ + 1 quà tặng Serum..."
Có thể: "...gồm 1 hũ Mặt Nạ Ngủ + 1 quà tặng Serum (vì chị mua combo 2)"
```

**Lý do chưa sửa:** Gift eligibility logic đã đúng, chỉ thiếu explanation. Improvement này cần thêm gift-reason context - nằm ngoài scope.

## 4. Greeting Flow

### Điểm mạnh ✅

**Pure greeting detection:**
```csharp
// SalesStateHandlerBase.cs:1572-1591
IsPureGreeting() detects: "hi", "hello", "alo", "chào", "chào em", "chào shop"
```

**Tự nhiên:** Bot phân biệt được pure greeting vs greeting + intent.

**Transition to consultation:**
```csharp
// SalesStateHandlerBase.cs:1569
return "Dạ em chào chị ạ. Hôm nay chị đang cần em tư vấn gì để em hỗ trợ mình nhanh nha?";
```

**Tự nhiên:** Greeting + immediate CTA, không để user phải hỏi lại "làm gì tiếp".

### Điểm cần cải thiện 🔶

**Có thể personalize cho returning customer:**
```
Hiện tại: "Dạ em chào chị ạ. Hôm nay chị đang cần..."
Có thể: "Dạ em chào chị ạ, chị quay lại rồi. Hôm nay chị cần..."
```

**Lý do chưa sửa:** Cần thêm returning-customer detection trong greeting handler. Nằm ngoài scope của transcript patch.

## 5. Multi-turn Conversation Coherence

### Điểm mạnh ✅

**Golden transcript test coverage:**
```csharp
// TranscriptGoldenFlowTests.cs:22-88
Turn 1: "cho em biết thêm về mặt nạ ngủ dưỡng ẩm"
Turn 2: "giá bao nhiêu vậy"
Turn 3: "có freeship ko e"
Turn 4: "ok vậy lấy sản phẩm này nhé"
Turn 5: "ok" (generic)
Turn 6: "đúng rồi" (explicit)
```

**Tự nhiên:** Bot duy trì context qua 6 turns, phân biệt được generic vs explicit confirmation.

**Semantic assertions prevent brittleness:**
```csharp
turn1.Should().ContainEquivalentOf("mat na ngu");  // không yêu cầu exact diacritics
turn4.Should().Contain("0911222333");              // exact match cho data
```

**Tự nhiên:** Tests verify semantic correctness chứ không phải exact wording, cho phép bot reply linh hoạt.

## Tổng kết

### Độ tự nhiên tổng thể: 8.5/10

**Điểm mạnh:**
- ✅ Phân biệt rõ generic vs explicit phrases
- ✅ Xử lý partial contact gracefully
- ✅ Product lock stable across policy/checkout turns
- ✅ Structured information breakdown
- ✅ Consistent tone (Dạ, ạ, mình)
- ✅ Multi-turn context coherence

**Điểm cần cải thiện (không critical):**
- 🔶 Wording có thể ngắn gọn hơn
- 🔶 Thiếu explicit acknowledgment cho product rejection
- 🔶 Thiếu gift eligibility explanation
- 🔶 Chưa personalize cho returning customer

**Kết luận:**
Conversation flow đã đạt mức "tự nhiên như sales thật" theo yêu cầu production. Các điểm cần cải thiện đều thuộc "nice-to-have" chứ không phải "must-have", và nằm ngoài scope của minimal-change patch hiện tại.

## Recommendations

1. **Short-term (không cần ngay):**
   - A/B test wording variations để tìm balance giữa clarity vs brevity
   - Collect user feedback về tone và wording

2. **Long-term (future iterations):**
   - Thêm rejection acknowledgment intent detection
   - Thêm gift eligibility explanation
   - Personalize greeting cho returning customers
   - Consider dynamic tone adjustment based on user's communication style

---

**Status:** Analysis complete
**Confidence:** HIGH (based on code review + golden transcript tests)
