# Phase 2: Integrate VIP Context into AI Prompt

**Duration:** 1 hour
**Priority:** P1
**Risk:** Medium
**Status:** ✅ Completed (2026-03-31)
**Dependencies:** Phase 1

---

## Overview

Move VIP lookup BEFORE AI call và integrate VIP context vào prompt để AI generate natural greeting thay vì prepend sau.

---

## Current State

**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:172-195`

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message)
{
    // Line 188: AI generates response WITHOUT VIP context
    var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

    // Line 189: Gets VIP greeting AFTER AI call (wasted)
    var vipGreeting = await GetVipGreetingAsync(ctx);

    // Line 194: Prepends VIP greeting
    return string.IsNullOrWhiteSpace(vipGreeting) ? reply : $"{vipGreeting}\n\n{reply}";
}
```

**Problem:** VIP greeting prepended SAU KHI AI tạo response → 2 tin nhắn rời rạc.

---

## Implementation

### Step 1: Move VIP Lookup Before AI Call (20min)

Update `BuildNaturalReplyAsync` method:

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message)
{
    var history = GetHistory(ctx);
    var productCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
    var contactSummary = GetContactSummary(ctx);

    // NEW: Get VIP profile BEFORE building prompt
    var vipProfile = await GetVipProfileAsync(ctx);
    var vipInstruction = BuildVipInstruction(vipProfile);

    var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
Thong tin da co: {contactSummary}
{vipInstruction}
Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.
- Phan cuoi phai huong ve xin thong tin con thieu de len don.
""";

    var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

    // No more prepending - AI handles VIP greeting naturally
    return response;
}
```

### Step 2: Add VIP Instruction Builder (15min)

Add helper method sau `BuildNaturalReplyAsync`:

```csharp
private static string BuildVipInstruction(VipProfile? vipProfile)
{
    if (vipProfile == null || !vipProfile.IsVip || string.IsNullOrWhiteSpace(vipProfile.GreetingStyle))
        return string.Empty;

    return $"""
Khach hang VIP:
- Dung giong dieu than mat, gan gui hon (VD: "chi iu", "chi yeu")
- Mo dau bang: "{vipProfile.GreetingStyle}"
- KHONG doi chinh sach gia, chi doi giong dieu
""";
}
```

### Step 3: Refactor GetVipProfileAsync (10min)

Update method để return `VipProfile?` thay vì `string`:

```csharp
private async Task<VipProfile?> GetVipProfileAsync(StateContext ctx)
{
    var customer = await CustomerIntelligenceService.GetExistingAsync(
        ctx.FacebookPSID,
        ctx.GetData<string>("facebookPageId"))
        ?? await CustomerIntelligenceService.GetOrCreateAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"),
            ctx.GetData<string>("customerPhone"));

    if (customer == null)
        return null;

    return await CustomerIntelligenceService.GetVipProfileAsync(customer);
}
```

### Step 4: Remove Old GetVipGreetingAsync Method (5min)

Delete method `GetVipGreetingAsync` (lines 197-209) - không còn cần thiết.

### Step 5: Update System Prompt Placeholder (10min)

Replace `{VIP_CONTEXT}` placeholder trong `sales-closer-system-prompt.txt` với instruction:

```
KHÁCH HÀNG HIỆN TẠI:
- Nếu có instruction về VIP, follow đúng giọng điệu và greeting style
- Nếu không có instruction VIP, dùng giọng điệu bình thường
```

---

## Expected Behavior

**Before (3 messages):**
```
Bot: "Chào chị yêu đã quay lại với Múi Xù ạ!"
Bot: "Em là trợ lý AI của Múi Xù..."
Bot: "Chị nhắn giúp em..."
```

**After (1 message):**
```
Bot: "Chào chị yêu đã quay lại với Múi Xù ạ! Dạ kem này phù hợp da dầu luôn chị ơi. Chị gửi em SĐT và địa chỉ để em lên đơn ngay nha."
```

---

## Testing

### Unit Tests (10min)

Add tests trong `ConsultingStateHandlerTests.cs`:

```csharp
[Fact]
public async Task BuildNaturalReply_VipCustomer_IntegratesGreetingNaturally()
{
    // Arrange
    var vipProfile = new VipProfile
    {
        IsVip = true,
        GreetingStyle = "Chao chi yeu da quay lai voi Mui Xu"
    };

    _mockCustomerService
        .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>()))
        .ReturnsAsync(vipProfile);

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Kem nay bao nhieu?");

    // Assert
    Assert.DoesNotContain("\n\n", response); // No disjointed parts
    Assert.Contains("chi yeu", response.ToLower()); // VIP tone
}

[Fact]
public async Task BuildNaturalReply_StandardCustomer_NoVipGreeting()
{
    // Arrange
    var standardProfile = new VipProfile { IsVip = false };

    _mockCustomerService
        .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>()))
        .ReturnsAsync(standardProfile);

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Ship bao lau?");

    // Assert
    Assert.DoesNotContain("chi yeu", response.ToLower());
    Assert.DoesNotContain("chi iu", response.ToLower());
}
```

---

## Success Criteria

- [x] VIP lookup happens BEFORE AI call
- [x] VIP instruction integrated vào prompt
- [x] AI generates natural greeting (không prepend)
- [x] Response là 1 message duy nhất (no `\n\n`)
- [x] VIP tone tự nhiên, không cứng nhắc
- [x] Standard customers không có VIP greeting
- [x] Unit tests pass

---

## Risk Assessment

**Risk:** Medium - changes core response generation logic

**Potential Issues:**
1. AI không follow VIP instruction → Monitor first 50 responses
2. VIP greeting không tự nhiên → Adjust instruction wording
3. Performance impact từ VIP lookup → Minimal (1 DB query)

**Mitigation:**
- Keep old method as `BuildNaturalReplyAsync_Legacy` for rollback
- Add logging để track VIP vs non-VIP responses
- Monitor response quality metrics

---

## Rollback Plan

1. Restore `BuildNaturalReplyAsync_Legacy` method
2. Revert `GetVipProfileAsync` signature
3. Restore `GetVipGreetingAsync` method
4. Estimated rollback time: 5 minutes

---

## Performance Impact

- **Before:** 1 VIP lookup AFTER AI call (wasted)
- **After:** 1 VIP lookup BEFORE AI call (used)
- **Net change:** 0 additional DB queries
- **Response time:** No change

---

## Next Steps

After Phase 2 complete:
- Proceed to Phase 3: Remove Hard-coded CTA
- Monitor VIP greeting integration quality
- Collect feedback từ first 50 VIP conversations
