# Fix Conversation State Reset After Order Complete

## Vấn đề phát hiện

Khi khách nhắn tin mới sau khi đã hoàn thành đơn hàng (state = `Complete`), bot không reset state về `Consulting` mà tiếp tục trả lời về draft order cũ.

**Hội thoại thực tế:**
```
Khách: hi sốp
Bot: Dạ don nhap DR-20260404155114-4D1DF7 cua chi dang cho ben em kiem tra lai thong tin nha.
```

**Mong đợi:**
```
Khách: hi sốp
Bot: Dạ chào chị ạ! Vui quá được gặp lại chị. Múi Xù chuyên các sản phẩm làm trắng da, trị nám, tàn nhang ạ. Chị đang quan tâm sản phẩm nào ạ?
```

## Phân tích nguyên nhân

### Root cause
`CompleteStateHandler` không có logic để nhận diện conversation mới và reset state. Khi khách nhắn tin sau khi hoàn thành đơn, bot vẫn ở state `Complete` và trả về thông báo về draft order cũ.

### Current behavior
**File:** `CompleteStateHandler.cs:50-61`
```csharp
protected override Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
{
    // Reset consultation rejection counter for next order
    ctx.SetData("consultationRejectionCount", 0);
    ctx.SetData("consultationDeclined", false);

    var draftCode = ctx.GetData<string>("draftOrderCode");
    var response = string.IsNullOrWhiteSpace(draftCode)
        ? "Dạ em da len don nhap cho chi roi a. Ben em se kiem tra lai thong tin va lien he xac nhan nha."
        : $"Dạ don nhap {draftCode} cua chi dang cho ben em kiem tra lai thong tin nha.";
    return Task.FromResult(response);
}
```

Vấn đề: Handler này luôn trả về thông báo về draft order, không kiểm tra xem khách có đang bắt đầu conversation mới không.

## Solution Design

### Option 1: Detect new conversation in CompleteStateHandler (RECOMMENDED)

Thêm logic nhận diện conversation mới dựa trên:
1. Tin nhắn là greeting ("hi", "hello", "chào")
2. Hoặc thời gian từ lần tương tác cuối > threshold (VD: 24h)

Nếu là conversation mới:
- Reset state về `Consulting`
- Clear draft order data
- Delegate sang `ConsultingStateHandler` để xử lý greeting

**Ưu điểm:**
- Đơn giản, tập trung logic ở 1 chỗ
- Không ảnh hưởng các handler khác
- Dễ test

**Nhược điểm:**
- Phải duplicate greeting detection logic

### Option 2: Add middleware to detect conversation reset

Tạo middleware chạy trước tất cả handlers để:
1. Kiểm tra xem có phải conversation mới không
2. Nếu có, reset state về `Consulting`
3. Delegate sang handler phù hợp

**Ưu điểm:**
- Centralized logic
- Áp dụng cho tất cả states

**Nhược điểm:**
- Phức tạp hơn
- Cần refactor state machine

### Decision: Option 1

Chọn Option 1 vì đơn giản và đủ để giải quyết vấn đề hiện tại.

## Implementation Steps

### Phase 1: Add conversation reset detection

**File:** `CompleteStateHandler.cs`

```csharp
protected override async Task<string> HandleInternalAsync(Models.StateContext ctx, string message)
{
    // Detect if this is a new conversation
    var isNewConversation = IsNewConversation(ctx, message);
    
    if (isNewConversation)
    {
        Logger.LogInformation("New conversation detected after Complete state for PSID: {PSID}, resetting to Consulting", ctx.FacebookPSID);
        
        // Reset state and clear draft order data
        ctx.CurrentState = ConversationState.Consulting;
        ctx.SetData("draftOrderId", null);
        ctx.SetData("draftOrderCode", null);
        ctx.SetData("selectedProductCodes", null);
        ctx.SetData("selectedGiftCode", null);
        ctx.SetData("selectedGiftName", null);
        ctx.SetData("shippingFee", null);
        ctx.SetData("vipGreetingSent", false); // Reset VIP greeting flag
        
        // Delegate to consulting handler for greeting
        return await HandleSalesConversationAsync(ctx, message);
    }
    
    // Original behavior for follow-up messages about the order
    ctx.SetData("consultationRejectionCount", 0);
    ctx.SetData("consultationDeclined", false);

    var draftCode = ctx.GetData<string>("draftOrderCode");
    var response = string.IsNullOrWhiteSpace(draftCode)
        ? "Dạ em da len don nhap cho chi roi a. Ben em se kiem tra lai thong tin va lien he xac nhan nha."
        : $"Dạ don nhap {draftCode} cua chi dang cho ben em kiem tra lai thong tin nha.";
    return response;
}

private bool IsNewConversation(Models.StateContext ctx, string message)
{
    // Check 1: Message is a greeting
    var greetings = new[] { "hi", "hello", "chao", "chào", "alo", "alô" };
    var normalizedMessage = message.Trim().ToLowerInvariant();
    var isGreeting = greetings.Any(g => normalizedMessage.StartsWith(g));
    
    if (isGreeting)
        return true;
    
    // Check 2: Time since last interaction > 24 hours
    var lastInteraction = ctx.GetData<DateTime?>("lastInteractionAt");
    if (lastInteraction.HasValue)
    {
        var hoursSinceLastInteraction = (DateTime.UtcNow - lastInteraction.Value).TotalHours;
        if (hoursSinceLastInteraction > 24)
        {
            Logger.LogInformation("Last interaction was {Hours}h ago for PSID: {PSID}, treating as new conversation", 
                hoursSinceLastInteraction, ctx.FacebookPSID);
            return true;
        }
    }
    
    return false;
}
```

### Phase 2: Update lastInteractionAt tracking

Đảm bảo `lastInteractionAt` được update mỗi khi có tin nhắn.

**File:** `SalesStateHandlerBase.cs` hoặc middleware

```csharp
// Update last interaction timestamp
ctx.SetData("lastInteractionAt", DateTime.UtcNow);
```

### Phase 3: Testing

**Test case 1: Greeting after order complete**
- Complete order → state = Complete
- Nhắn "hi sốp" → reset về Consulting, chào VIP
- Verify: draft order data cleared, VIP greeting sent

**Test case 2: Follow-up about order**
- Complete order → state = Complete
- Nhắn "đơn hàng của em thế nào rồi" → giữ state Complete, trả lời về draft
- Verify: không reset state

**Test case 3: Time-based reset**
- Complete order → state = Complete
- Đợi > 24h (hoặc mock time)
- Nhắn bất kỳ → reset về Consulting
- Verify: conversation reset

## Success Criteria

✅ Khách nhắn greeting sau khi hoàn thành đơn → bot reset state và chào lại
✅ Khách hỏi về đơn hàng sau khi hoàn thành → bot giữ state và trả lời về draft
✅ Sau 24h không tương tác → tin nhắn bất kỳ đều reset conversation
✅ VIP greeting flag được reset khi conversation mới bắt đầu

## Risk Assessment

**Low risk:**
- Chỉ sửa `CompleteStateHandler`
- Không ảnh hưởng flow khác
- Có thể rollback dễ dàng

**Cần chú ý:**
- Đảm bảo không reset conversation khi khách đang hỏi về đơn hàng
- Test kỹ time-based reset để tránh reset quá sớm
- Verify draft order data được clear đúng cách

## Alternative Approaches

### Approach 2: State expiration
Thêm TTL cho state `Complete`, tự động expire sau 24h.

**Pros:** Automatic, không cần check manual
**Cons:** Phức tạp hơn, cần background job

### Approach 3: Explicit "new order" command
Yêu cầu khách nói "đặt hàng mới" để reset.

**Pros:** Rõ ràng, không nhầm lẫn
**Cons:** Không tự nhiên, khách phải biết command
