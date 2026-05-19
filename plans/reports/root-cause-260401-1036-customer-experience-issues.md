# Root Cause Analysis: Customer Experience Issues

**Date:** 2026-04-01
**Conversation:** Returning customer asking about freeship

## Hội thoại có vấn đề

```
khách: hi
bot: Chào chị, em thấy chị ghé lại rồi ạ. Chị muốn tìm hiểu thêm về sản phẩm nào bên em ạ?
khách: bên em có sản phẩm nào ha
bot: Bên em có các dòng sản phẩm làm trắng da, trị nám và tàn nhang hiệu quả lắm chị ạ. Chị có đang quan tâm đến vấn đề nào cụ thể không ạ?
khách: chị đi nắng sợ đen da thì sử dụng sản phẩm nào vậy em
bot: Dạ để bảo vệ da khỏi tác hại của ánh nắng, chị nên dùng kem chống nắng vật lý Múi Xù ạ. Sản phẩm này giúp nâng tone nhẹ nhàng và kiềm dầu tốt lắm chị. Chị iu cho em xin số điện thoại và địa chỉ em lên đơn luôn nha.
khách: chị là khách cũ mà em cũng không có thông tin mà phải xin?
bot: Dạ em xin lỗi chị, em kiểm tra lại thông tin khách hàng cũ của mình nha. Chị cho em xin lại SĐT để em tìm lại thông tin ạ.
khách: 0888129403
bot: Dạ em tìm thấy thông tin của mình rồi ạ. Em lên đơn chống nắng vật lý Múi Xù cho chị ngay nha.
khách: có freeship không em?
bot: Dạ phí ship theo chính sách của shop ạ. Em lên đơn ngay cho chị, chị gửi em SĐT và địa chỉ nha.
```

## Vấn đề phát hiện

### 1. Bot không nhớ thông tin khách cũ
- **Triệu chứng:** Khách nói "chị là khách cũ" nhưng bot vẫn hỏi SĐT và địa chỉ
- **Hành vi mong đợi:** Bot nên tự động load thông tin từ database và confirm với khách

### 2. Bot mâu thuẫn
- **Triệu chứng:** Bot nói "em tìm thấy thông tin" nhưng vẫn hỏi lại SĐT và địa chỉ ở cuối
- **Hành vi mong đợi:** Nếu đã có thông tin thì không hỏi lại

### 3. Bot không trả lời câu hỏi freeship
- **Triệu chứng:** Khách hỏi "có freeship không?" nhưng bot chỉ nói "phí ship theo chính sách" rồi lại hỏi SĐT/địa chỉ
- **Hành vi mong đợi:** Bot nên trả lời rõ ràng: "Có freeship nếu mua 2 sản phẩm" hoặc "Phí ship 30k cho 1 sản phẩm"

## Root Cause Analysis

### Issue 1: Bot không nhớ khách cũ

**Root Cause:**
- Code có logic load thông tin khách cũ (`GetVipProfileAsync`, `contactNeedsConfirmation`)
- Nhưng logic này chỉ trigger khi:
  1. Customer đã có trong database
  2. Bot detect được phone/address từ message
  3. Set flag `contactNeedsConfirmation = true`

**Vấn đề:**
```csharp
// File: SalesStateHandlerBase.cs:385-409
if (needsConfirmation && missingInfo.Count == 0)
{
    // Only push for confirmation if customer is ready to buy or browsing products
    if (intent == Services.AI.Models.CustomerIntent.ReadyToBuy ||
        intent == Services.AI.Models.CustomerIntent.Browsing)
    {
        return $"""
CTA Instruction: Customer is returning - their info was loaded from previous orders.
Naturally confirm their existing info before creating order.
"Em thay chi da dat hang truoc day roi a. Chi van dung SDT {phone} va dia chi {address} dung khong a?"
""";
    }
}
```

**Phân tích:**
1. Logic này chỉ chạy khi `needsConfirmation = true` và `missingInfo.Count == 0`
2. Nhưng trong hội thoại, bot không detect được phone/address từ message "hi" → không set flag
3. Kết quả: Bot không biết khách là khách cũ

**Evidence từ code:**
```csharp
// File: ConversationStateMachine.cs:288
context.SetData("contactNeedsConfirmation", true);
```
Flag này chỉ được set khi load thông tin từ database, nhưng cần trigger point để load.

### Issue 2: Bot mâu thuẫn (nói tìm thấy nhưng vẫn hỏi)

**Root Cause:**
- AI response generation không consistent với state
- Bot nói "em tìm thấy thông tin" là do AI generate response dựa trên context
- Nhưng CTA instruction vẫn yêu cầu hỏi thông tin vì `missingInfo.Count > 0`

**Vấn đề:**
```csharp
// File: SalesStateHandlerBase.cs:420-427
if (hasProduct)
{
    var missing = string.Join(" va ", missingInfo);
    return $"""
CTA Instruction: Naturally ask customer to provide missing info ({missing}) to complete the order.
Use friendly tone like "Chi gui em {missing} de em len don nha"
""";
}
```

**Phân tích:**
1. Khi khách cung cấp SĐT "0888129403", bot capture được phone
2. Nhưng vẫn thiếu address → `missingInfo` vẫn có "dia chi"
3. CTA instruction yêu cầu hỏi địa chỉ
4. AI generate response "em tìm thấy thông tin" (dựa trên context có phone) nhưng vẫn hỏi địa chỉ (theo CTA instruction)

### Issue 3: Bot không trả lời câu hỏi freeship

**Root Cause:**
- AI intent detection classify "có freeship không?" là `Questioning`
- Nhưng AI response generation không có context về freeship policy

**Vấn đề:**
```csharp
// File: SalesStateHandlerBase.cs:303-306
Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.
```

**Phân tích:**
1. Prompt có rule "Khong tu y them qua, freeship, giam gia" → AI không được tự ý nói về freeship
2. Nhưng khi khách HỎI về freeship, AI nên trả lời
3. AI không có context về freeship policy cụ thể:
   - Freeship khi mua 2 sản phẩm hoặc combo
   - Phí ship 30k cho 1 sản phẩm

**Evidence từ code:**
```csharp
// File: FreeshipCalculator.cs:11-19
public bool IsEligibleForFreeship(List<string> productCodes)
{
    return productCodes.Count >= 2 ||
           productCodes.Any(code => code.Equals(ComboProductCode, StringComparison.OrdinalIgnoreCase));
}
```

Logic freeship có sẵn nhưng không được pass vào AI prompt.

## Tổng kết Root Causes

| Issue | Root Cause | Impact |
|-------|-----------|--------|
| Không nhớ khách cũ | Logic load thông tin khách cũ không trigger khi khách chỉ nói "hi" | High - Khách cảm thấy không được quan tâm |
| Bot mâu thuẫn | AI response không sync với CTA instruction khi có partial info | Medium - Khách bối rối |
| Không trả lời freeship | AI prompt thiếu freeship policy context | High - Khách không được thông tin cần thiết |

## Giải pháp đề xuất

### Fix 1: Proactive customer recognition
- Khi khách nói "hi", check database xem có thông tin cũ không
- Nếu có, load và set `contactNeedsConfirmation = true`
- Bot chủ động confirm: "Em thấy chị đã đặt hàng trước đây, chị vẫn dùng SĐT xxx và địa chỉ yyy đúng không ạ?"

### Fix 2: Sync AI response với state
- Khi có partial info (có phone nhưng thiếu address), AI nên nói rõ:
  - "Em có SĐT của chị rồi, chị gửi em địa chỉ để em lên đơn nha"
- Không nói "em tìm thấy thông tin" khi vẫn thiếu info

### Fix 3: Add freeship policy vào AI prompt
- Thêm freeship context vào prompt:
  ```
  Freeship policy:
  - Mien phi van chuyen khi mua 2 san pham tro len hoac combo
  - Phi van chuyen: 30,000d cho 1 san pham
  ```
- Khi khách hỏi về freeship, AI trả lời dựa trên policy này

## Unresolved Questions

1. Làm sao trigger logic load thông tin khách cũ khi khách chỉ nói "hi"?
   - Cần check database by PSID ngay từ đầu?
   - Hay đợi khách mention về đơn hàng cũ?

2. Khi nào nên proactive confirm thông tin khách cũ?
   - Ngay khi khách nói "hi"?
   - Hay đợi khách chọn sản phẩm?

3. Freeship policy có thay đổi theo thời gian không?
   - Nếu có, cần load từ config hay hardcode trong prompt?
