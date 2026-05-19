# Báo Cáo Đánh Giá: Code Hiện Tại vs Requirements Khách Hàng

**Ngày:** 2026-03-31
**Người đánh giá:** Code Reviewer Agent
**Phạm vi:** So sánh implementation hiện tại với yêu cầu từ khách hàng Múi Xù

---

## Tóm Tắt Đánh Giá

**Kết luận chung:** Code hiện tại đã implement **70-75%** requirements. Còn thiếu một số tính năng quan trọng và cần điều chỉnh logic để phù hợp hoàn toàn với yêu cầu khách hàng.

**Điểm mạnh:**
- ✅ Đã có foundation tốt cho sales-first chatbot
- ✅ Đã implement risk detection và VIP recognition
- ✅ Đã có human handoff mechanism
- ✅ Đã có draft order workflow

**Điểm yếu:**
- ❌ Chưa có livestream comment automation
- ❌ Chưa có email notification cho nhân viên
- ❌ Bot lock mechanism chưa hoàn chỉnh
- ❌ Prompt chưa đủ aggressive về việc "ép chốt"

---

## Chi Tiết Đánh Giá Từng Yêu Cầu

### 1. ✅ Quick Reply từ Facebook Ads (HOÀN THÀNH)

**Yêu cầu:**
- Khách click vào 3 lựa chọn: "Kem Chống Nắng", "Kem Lụa", "2 sản phẩm freeship"
- Bot reply: Sản phẩm + Quà tặng + "Chị iu cho em xin số điện thoại và địa chỉ"

**Implementation hiện tại:**
```csharp
// QuickReplySalesStateHandler.cs - ✅ Đã implement
// ProductMappingService.cs - ✅ Map payload to product
// GiftSelectionService.cs - ✅ Select gift by priority
// FreeshipCalculator.cs - ✅ Calculate shipping fee
```

**Đánh giá:** ✅ **PASS** - Đã implement đầy đủ

---

### 2. ⚠️ Luồng Check Tỷ Lệ Nhận Hàng (HOÀN THÀNH NHƯNG CẦN ĐIỀU CHỈNH)

**Yêu cầu:**
- AI KHÔNG được tự ý hủy đơn hay đòi chuyển khoản trước
- AI chỉ gắn Tag/Cảnh báo đỏ vào đơn
- Nhân viên sẽ gọi điện xác nhận

**Implementation hiện tại:**
```csharp
// CustomerIntelligenceService.cs:106-138
public async Task<RiskSignal> BuildRiskSignalAsync(...)
{
    var riskScore = insight?.RiskScore ?? localRiskScore;
    var riskLevel = riskScore >= _options.HighRiskThreshold
        ? RiskLevel.High : ...;

    signal.RequiresManualReview = riskLevel == RiskLevel.High;
    // ✅ Đúng: Chỉ flag, không reject
}

// DraftOrderService.cs:98-101
draftOrder.RiskLevel = riskSignal.Level;
draftOrder.RequiresManualReview = true; // ✅ Luôn require review
```

**Vấn đề phát hiện:**
```csharp
// SalesStateHandlerBase.cs:210-216
private static string BuildDraftConfirmation(DraftOrder draftOrder)
{
    var riskLine = draftOrder.RiskLevel == RiskLevel.High
        ? "Don nay se duoc nhan vien goi xac nhan ky hon truoc khi giao nha."
        : "Ben em se co ban kiem tra lai thong tin va chot giao hang cho minh nha.";
    // ⚠️ Vấn đề: Bot đang nói với khách về risk level
    // Khách hàng không cần biết họ bị đánh giá là "high risk"
}
```

**Đánh giá:** ⚠️ **NEEDS ADJUSTMENT** - Logic đúng nhưng message cần điều chỉnh

**Khuyến nghị:**
- Đổi message để không để lộ việc khách bị đánh giá risk
- Tất cả đơn đều nói: "Ben em se co ban kiem tra lai thong tin va chot giao hang cho minh nha."

---

### 3. ✅ Luồng Nhận Diện Khách Cũ/VIP (HOÀN THÀNH)

**Yêu cầu:**
- Check Nobita để biết khách VIP
- Đổi văn phong thân mật hơn
- KHÔNG được tự ý hứa freeship/quà ngoài policy

**Implementation hiện tại:**
```csharp
// CustomerIntelligenceService.cs:75-104
public async Task<VipProfile> GetVipProfileAsync(...)
{
    vipProfile.IsVip = insight?.IsVip == true ||
                       vipProfile.TotalOrders >= _options.VipOrderThreshold;
    vipProfile.GreetingStyle = vipProfile.IsVip
        ? "Da em chao chi khach quen cua Mui Xu a."
        : vipProfile.TotalOrders > 0
            ? "Da em chao chi, em ho tro chi tiep nha."
            : string.Empty;
    // ✅ Chỉ đổi greeting, không đổi policy
}

// SalesStateHandlerBase.cs:198-208
private async Task<string> GetVipGreetingAsync(StateContext ctx)
{
    var vipProfile = await CustomerIntelligenceService.GetVipProfileAsync(customer);
    return vipProfile.GreetingStyle; // ✅ Chỉ dùng để chào
}
```

**Đánh giá:** ✅ **PASS** - Đã implement đúng yêu cầu

---

### 4. ⚠️ Prompt "Ép Chốt" (CẦN TĂNG CƯỜNG)

**Yêu cầu:**
- Dù đang trả lời gì, phần đuôi LUÔN LUÔN phải xin SĐT + địa chỉ
- Không bao giờ để đoạn chat kết thúc lửng lơ

**Implementation hiện tại:**
```csharp
// SalesStateHandlerBase.cs:223-229
private static string AppendCallToAction(string response, string cta)
{
    var safeResponse = string.IsNullOrWhiteSpace(response)
        ? "Dạ em ho tro chi ngay day a." : response.Trim();
    return safeResponse.Contains(cta, StringComparison.OrdinalIgnoreCase)
        ? safeResponse
        : $"{safeResponse}\n\n{cta}"; // ✅ Có append CTA
}

// SalesStateHandlerBase.cs:172-195
private async Task<string> BuildNaturalReplyAsync(...)
{
    var cta = HasSelectedProduct(ctx)
        ? SalesMessageParser.BuildMissingInfoPrompt(ctx)
        : "Chi nhan giup em Kem Chong Nang, Kem Lua hay combo 2 san pham de em len don nhanh nha.";
    var reply = AppendCallToAction(response, cta);
    // ✅ Có logic append CTA
}
```

**Vấn đề phát hiện:**
```txt
// sales-closer-system-prompt.txt:1-20
Mục tiêu tối thượng:
- Trả lời tự nhiên như người thật.
- Dù đang giải thích gì, luôn kéo cuộc trò chuyện về việc xin số điện thoại và địa chỉ để lên đơn.
- Ưu tiên chốt đơn nhanh, không lan man như tư vấn routine chăm da chuyên sâu.

⚠️ Vấn đề: Prompt chưa đủ aggressive
- Chưa có instruction rõ ràng về việc LUÔN LUÔN kết thúc bằng CTA
- Chưa có ví dụ cụ thể về cách "ép chốt" tự nhiên
```

**Đánh giá:** ⚠️ **NEEDS IMPROVEMENT** - Logic có nhưng prompt chưa đủ mạnh

**Khuyến nghị:**
- Tăng cường system prompt với instruction rõ ràng hơn
- Thêm ví dụ cụ thể về cách kết thúc mọi câu trả lời bằng CTA
- Có thể cần thêm post-processing để đảm bảo 100% response có CTA

---

### 5. ✅ Policy Guard (HOÀN THÀNH)

**Yêu cầu:**
- Bot KHÔNG được tự ý hứa: freeship, quà, giảm giá, hủy đơn, hoàn tiền
- Nếu khách yêu cầu → escalate to human

**Implementation hiện tại:**
```csharp
// PolicyGuardService.cs:9-19
private static readonly (string Keyword, SupportCaseReason Reason)[] BuiltInKeywords =
{
    ("huy don", SupportCaseReason.CancellationRequest),
    ("hoan tien", SupportCaseReason.RefundRequest),
    ("mien phi van chuyen", SupportCaseReason.PolicyException),
    ("them khuyen mai", SupportCaseReason.PolicyException),
    ("giam gia them", SupportCaseReason.PolicyException),
    // ✅ Đã cover các keyword nguy hiểm
};

// SalesStateHandlerBase.cs:76-90
var decision = PolicyGuardService.Evaluate(message);
if (decision.RequiresEscalation)
{
    var supportCase = await CaseEscalationService.EscalateAsync(...);
    ctx.CurrentState = ConversationState.HumanHandoff;
    // ✅ Đúng: Escalate thay vì tự xử lý
}
```

**Đánh giá:** ✅ **PASS** - Đã implement đúng yêu cầu

---

### 6. ⚠️ Human Handoff với Email Notification (CHƯA HOÀN CHỈNH)

**Yêu cầu:**
- Khi escalate → gửi email cho nhân viên
- Bot KHÔNG trả lời cùng phiên với nhân viên
- Nhân viên click button "Hoàn thành" trong email → bot mới tiếp tục

**Implementation hiện tại:**
```csharp
// CaseEscalationService.cs:34-66
public async Task<HumanSupportCase> EscalateAsync(...)
{
    _dbContext.HumanSupportCases.Add(supportCase);
    await _dbContext.SaveChangesAsync(cancellationToken);
    await _botLockService.LockAsync(facebookPsid, summary, supportCase.Id, cancellationToken);
    await NotifyAssignedManagerAsync(supportCase, cancellationToken); // ✅ Có gửi email
    return supportCase;
}

// BotLockService.cs:26-62
public async Task<bool> IsLockedAsync(string facebookPsid, ...)
{
    return await _dbContext.BotConversationLocks
        .AnyAsync(x => x.FacebookPSID == facebookPsid && x.IsLocked, ...);
    // ✅ Có check lock
}
```

**Vấn đề phát hiện:**
```csharp
// EmailNotificationService.cs - ❌ CHƯA IMPLEMENT
// Chỉ có interface, chưa có implementation thực tế
// Chưa có email template
// Chưa có button "Hoàn thành case" trong email
```

**Đánh giá:** ⚠️ **PARTIALLY IMPLEMENTED** - Logic có nhưng email chưa hoàn chỉnh

**Khuyến nghị:**
- Implement EmailNotificationService với SMTP
- Tạo email template với button "Complete Case"
- Button link đến endpoint: `/api/internal/support-cases/{caseId}/complete`
- Endpoint này sẽ gọi `BotLockService.ReleaseAsync()`

---

### 7. ❌ Livestream Comment Automation (CHƯA IMPLEMENT)

**Yêu cầu:**
- Tự động nhắn tin cho khách khi có comment trên livestream
- Ẩn comment của khách sau khi nhắn tin

**Implementation hiện tại:**
```csharp
// LiveCommentAutomationService.cs:1-10
public class LiveCommentAutomationService : ILiveCommentAutomationService
{
    public Task<bool> ShouldHandleCommentAsync(string commentText, ...)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(commentText));
        // ❌ Chỉ là stub, chưa có logic thực tế
    }
}
```

**Đánh giá:** ❌ **NOT IMPLEMENTED** - Chỉ có interface, chưa có implementation

**Khuyến nghị:**
- Implement Facebook Graph API để:
  - Subscribe to live video comments webhook
  - Send private message to commenter
  - Hide comment after sending message
- Cần thêm configuration cho livestream automation
- Cần test với Facebook Live Video API

---

### 8. ✅ Draft Order Workflow (HOÀN THÀNH)

**Yêu cầu:**
- Tạo đơn nháp local trước
- Nhân viên check lại thông tin
- Sau đó mới submit lên Nobita

**Implementation hiện tại:**
```csharp
// DraftOrderService.cs:30-113
public async Task<DraftOrder> CreateFromContextAsync(StateContext context, ...)
{
    // ✅ Validate có product và contact info
    if (productCodes.Count == 0) throw new InvalidOperationException(...);
    if (string.IsNullOrWhiteSpace(phoneNumber) || ...) throw new InvalidOperationException(...);

    // ✅ Tạo draft order local
    var draftOrder = new DraftOrder { ... };
    _dbContext.DraftOrders.Add(draftOrder);

    // ✅ Build risk signal
    var riskSignal = await _customerIntelligenceService.BuildRiskSignalAsync(...);
    draftOrder.RequiresManualReview = true; // ✅ Luôn require review

    return draftOrder;
}

// NobitaSubmissionService.cs - ✅ Có service riêng để submit lên Nobita
// AdminDashboardQueryService.cs - ✅ Có dashboard để nhân viên review
```

**Đánh giá:** ✅ **PASS** - Đã implement đúng workflow

---

### 9. ✅ Multi-Branch Support (ĐÃ CÓ FOUNDATION)

**Yêu cầu:**
- Mỗi chi nhánh là 1 page riêng
- 1 người quản lý riêng

**Implementation hiện tại:**
```csharp
// Tenant.cs, FacebookPageConfig.cs - ✅ Có multi-tenant architecture
// TenantResolutionMiddleware.cs - ✅ Resolve tenant by page ID
// ManagerProfile.cs - ✅ Có manager per tenant
```

**Đánh giá:** ✅ **PASS** - Foundation đã sẵn sàng cho multi-branch

---

### 10. ⚠️ Bot Behavior - Tư Vấn vs Bán Hàng (CẦN ĐIỀU CHỈNH)

**Yêu cầu khách hàng:**
> "Cần bot tư vấn cần bán được hàng. Chứ đúng quá khô khan cũng không hay"
> "Bên mình chuyên làm trắng, trị nám, trị tàn nhang, tệp 30+"
> "10 khách inb thì 7 khách đưa địa chỉ rồi, 3 khách mới hỏi kỹ"
> "Không cần tư vấn loại da hay bot là chuyên gia tư vấn nữa"

**Implementation hiện tại:**
```txt
// sales-closer-system-prompt.txt
Bạn là trợ lý bán hàng của Múi Xù.
Mục tiêu tối thượng:
- Trả lời tự nhiên như người thật.
- Ưu tiên chốt đơn nhanh, không lan man như tư vấn routine chăm da chuyên sâu.

⚠️ Vấn đề: Prompt chưa đủ specific về target audience và tone
- Chưa mention về target audience là phụ nữ 30+
- Chưa có instruction về cách trả lời ngắn gọn, không lan man
- Chưa có ví dụ về cách balance giữa tư vấn và bán hàng
```

**Đánh giá:** ⚠️ **NEEDS IMPROVEMENT** - Cần điều chỉnh prompt và tone

**Khuyến nghị:**
- Update system prompt với context về target audience (phụ nữ 30+, làm trắng/trị nám)
- Thêm instruction: "Trả lời ngắn gọn, 2-3 câu, không lan man"
- Thêm ví dụ về cách trả lời FAQ một cách tự nhiên nhưng vẫn hướng về chốt đơn
- Có thể cần fine-tune hoặc few-shot examples

---

## Tổng Kết Gaps

### ❌ Critical Gaps (Cần implement ngay)

1. **Email Notification System**
   - File: `EmailNotificationService.cs`
   - Status: Interface có, implementation chưa có
   - Impact: Nhân viên không nhận được thông báo khi có case cần xử lý

2. **Livestream Comment Automation**
   - File: `LiveCommentAutomationService.cs`
   - Status: Stub only
   - Impact: Không tự động nhắn tin cho khách comment trên livestream

### ⚠️ Important Gaps (Cần điều chỉnh)

3. **System Prompt Enhancement**
   - File: `sales-closer-system-prompt.txt`
   - Issue: Chưa đủ aggressive về "ép chốt", chưa có context về target audience
   - Impact: Bot có thể không đủ effective trong việc chốt đơn

4. **Risk Message to Customer**
   - File: `SalesStateHandlerBase.cs:210-216`
   - Issue: Đang để lộ việc khách bị đánh giá risk
   - Impact: Khách hàng có thể cảm thấy không thoải mái

5. **Bot Lock Mechanism**
   - File: `BotLockService.cs`, `HumanHandoffStateHandler.cs`
   - Issue: Chưa có mechanism để unlock khi nhân viên hoàn thành case
   - Impact: Bot có thể bị lock vĩnh viễn nếu nhân viên quên unlock

### ✅ Implemented Well

- Quick Reply Handler với product mapping, gift selection, freeship
- Risk detection và VIP recognition
- Policy guard để prevent bot tự ý hứa hẹn
- Draft order workflow với manual review
- Multi-tenant foundation

---

## Khuyến Nghị Ưu Tiên

### Phase 1: Critical Fixes (1-2 ngày)

1. **Implement Email Notification**
   - SMTP configuration
   - Email template với button "Complete Case"
   - Endpoint để handle case completion

2. **Fix Risk Message**
   - Đổi message để không để lộ risk level
   - Tất cả đơn đều dùng message neutral

3. **Enhance System Prompt**
   - Thêm context về target audience
   - Thêm instruction về "ép chốt" aggressive hơn
   - Thêm few-shot examples

### Phase 2: Important Features (3-5 ngày)

4. **Implement Livestream Automation**
   - Facebook Graph API integration
   - Comment webhook handler
   - Auto-hide comment after messaging

5. **Complete Bot Lock Mechanism**
   - Email button → unlock endpoint
   - Auto-unlock after timeout
   - Dashboard để nhân viên manual unlock

### Phase 3: Optimization (1-2 tuần)

6. **Fine-tune Bot Behavior**
   - Collect real conversation data
   - Analyze conversion rate
   - Adjust prompt based on data

7. **Add Analytics**
   - Track contact capture rate
   - Track draft order rate
   - Track handoff rate

---

## Kết Luận

Code hiện tại đã có **foundation rất tốt** và implement được **70-75%** requirements. Các gaps chính là:

1. ❌ Email notification chưa hoàn chỉnh
2. ❌ Livestream automation chưa có
3. ⚠️ System prompt cần tăng cường
4. ⚠️ Một số message cần điều chỉnh

**Ước tính thời gian để hoàn thiện:** 1-2 tuần

**Rủi ro:** Thấp - Các gaps chủ yếu là feature bổ sung, không ảnh hưởng đến core functionality đã có.

**Khuyến nghị:** Có thể deploy pilot với 1 page để test, trong khi tiếp tục develop các missing features.
