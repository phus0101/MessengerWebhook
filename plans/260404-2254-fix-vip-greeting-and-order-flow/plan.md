# Fix VIP Greeting và Order Flow Issues

## Vấn đề phát hiện

### 1. Bot chào lại khách VIP 2 lần
```
Tin nhắn 1: "Dạ em chào chị ạ. Múi Xù chuyên..."
Tin nhắn 2: "Dạ em chào chị. Kem Chống Nắng..."  ❌
```

### 2. Bot không trả lời câu hỏi về freeship
```
Khách: "có freeship ko em"
Bot: "Dạ em da len don nhap DR-..." ❌
```

### 3. Bot lên đơn khi chưa xác nhận thông tin
- Khách cũ có remembered contact trong DB
- Bot tự động lên đơn mà chưa hỏi xác nhận SĐT/địa chỉ

## Phân tích nguyên nhân

### Issue 1: Chào 2 lần
- VIP instruction được inject vào mỗi tin nhắn
- Logic `isFirstMessage` chỉ kiểm tra history count
- Không track xem đã chào VIP chưa

### Issue 2: Bỏ qua câu hỏi freeship
- Logic auto-create draft order chạy trước khi AI reply
- Khi có đủ contact + product → tạo draft ngay
- AI không có cơ hội trả lời câu hỏi

### Issue 3: Lên đơn thiếu xác nhận
- `contactNeedsConfirmation=true` nhưng không được enforce
- Logic `HasRequiredContact` chỉ check có data, không check confirmed
- Auto-create draft order bypass confirmation step

## Solution Design

### Fix 1: Track VIP greeting state
**File:** `SalesStateHandlerBase.cs`

```csharp
// Thêm flag tracking
if (isFirstMessage && vipProfile?.IsVip == true)
{
    ctx.SetData("vipGreetingSent", true);
}

// Chỉ inject VIP greeting nếu chưa chào
var shouldGreet = isFirstMessage && 
                  vipProfile?.IsVip == true && 
                  ctx.GetData<bool?>("vipGreetingSent") != true;
```

### Fix 2: Delay draft order creation
**File:** `SalesStateHandlerBase.cs:HandleSalesConversationAsync`

**Hiện tại:**
```csharp
// Auto-create draft order when customer provides all required info
if (hasContact && !hasProduct) { ... }
if (hasContact && hasProduct) {
    // Tạo draft ngay ❌
}
```

**Sửa thành:**
```csharp
// Chỉ tạo draft khi:
// 1. Có đủ contact + product
// 2. Khách đã xác nhận (không còn contactNeedsConfirmation)
// 3. Intent = ReadyToBuy hoặc đã hỏi đủ thông tin
if (hasContact && hasProduct && !needsConfirmation) {
    // Tạo draft
}
```

### Fix 3: Enforce confirmation requirement
**File:** `SalesMessageParser.cs:HasRequiredContact`

**Hiện tại:**
```csharp
public static bool HasRequiredContact(StateContext context)
{
    return !string.IsNullOrWhiteSpace(context.GetData<string>("customerPhone")) &&
           !string.IsNullOrWhiteSpace(context.GetData<string>("shippingAddress")) &&
           !NeedsContactConfirmation(context);
}
```

**Giữ nguyên** - Logic này đúng rồi, vấn đề là ở chỗ auto-create draft order

## Implementation Steps

### Phase 1: Fix VIP greeting duplication
1. Đọc `SalesStateHandlerBase.cs:BuildNaturalReplyAsync`
2. Thêm logic track `vipGreetingSent` flag
3. Chỉ inject VIP greeting khi chưa chào
4. Test: Khách VIP nhắn "hi" → chào 1 lần, tin nhắn sau không chào lại

### Phase 2: Fix draft order timing
1. Đọc `SalesStateHandlerBase.cs:HandleSalesConversationAsync`
2. Tìm đoạn auto-create draft order
3. Thêm điều kiện: chỉ tạo khi `!needsConfirmation`
4. Đảm bảo AI có cơ hội reply trước khi tạo draft
5. Test: Khách hỏi freeship → bot trả lời trước, chưa lên đơn

### Phase 3: Update system prompt
1. Đọc `sales-closer-system-prompt.txt`
2. Thêm hướng dẫn xử lý câu hỏi freeship/policy
3. Nhấn mạnh: trả lời câu hỏi trước, sau đó mới hỏi thông tin

### Phase 4: Testing
1. Test case 1: Khách VIP mới
   - "hi sốp" → chào VIP 1 lần
   - "muốn mua kem" → không chào lại
   
2. Test case 2: Khách hỏi freeship
   - "có freeship ko" → trả lời về policy
   - Chưa tạo draft order
   
3. Test case 3: Khách cũ với remembered contact
   - "hi" → chào VIP + load contact
   - "muốn mua kem" → hỏi xác nhận thông tin
   - "đúng rồi" → mới tạo draft

## Success Criteria

✅ Bot chỉ chào VIP 1 lần trong conversation
✅ Bot trả lời câu hỏi freeship/policy trước khi lên đơn
✅ Bot hỏi xác nhận remembered contact trước khi tạo draft
✅ Flow tự nhiên: chào → tư vấn → trả lời câu hỏi → xác nhận → lên đơn

## Risk Assessment

**Low risk:**
- Chỉ sửa logic flow, không đụng database
- Có thể rollback dễ dàng
- Test locally trước khi deploy

**Cần chú ý:**
- Đảm bảo không break existing flow cho khách mới
- Kiểm tra edge case: khách cũ nhưng đổi SĐT/địa chỉ
