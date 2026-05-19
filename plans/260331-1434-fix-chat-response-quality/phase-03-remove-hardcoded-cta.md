# Phase 3: Remove Hard-coded CTA Logic

**Duration:** 45 minutes
**Priority:** P1
**Risk:** Low
**Status:** ✅ Completed (2026-03-31)
**Dependencies:** Phase 2

---

## Overview

Thay thế hard-coded CTA bằng dynamic CTA context được pass vào AI prompt để AI generate natural CTA.

---

## Current State

**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs:190-193`

```csharp
// Line 190-192: Hard-coded CTA
var cta = HasSelectedProduct(ctx)
    ? SalesMessageParser.BuildMissingInfoPrompt(ctx)
    : "Chi nhan giup em Kem Chong Nang, Kem Lua hay combo 2 san pham de em len don nhanh nha.";

// Line 193: Appends CTA mechanically
var reply = AppendCallToAction(response, cta);
```

**Problem:** CTA được append cứng nhắc sau AI response → không tự nhiên.

---

## Implementation

### Step 1: Build CTA Context (15min)

Add helper method để build CTA instruction cho AI:

```csharp
private static string BuildCtaContext(StateContext ctx)
{
    var hasProduct = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
    var missingInfo = GetMissingContactInfo(ctx);

    if (missingInfo.Count == 0)
    {
        return """
Ket thuc: Moi khach xac nhan thong tin de len don.
""";
    }

    if (hasProduct)
    {
        var missing = string.Join(" va ", missingInfo);
        return $"""
Ket thuc: Moi khach gui {missing} de len don.
""";
    }

    return """
Ket thuc: Moi khach chon san pham (Kem Chong Nang, Kem Lua, hoac combo) va gui thong tin de len don.
""";
}

private static List<string> GetMissingContactInfo(StateContext ctx)
{
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone")))
        missing.Add("so dien thoai");
    if (string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress")))
        missing.Add("dia chi");
    return missing;
}
```

### Step 2: Integrate CTA Context into Prompt (15min)

Update `BuildNaturalReplyAsync` method:

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message)
{
    var history = GetHistory(ctx);
    var productCodes = ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>();
    var contactSummary = GetContactSummary(ctx);

    var vipProfile = await GetVipProfileAsync(ctx);
    var vipInstruction = BuildVipInstruction(vipProfile);

    // NEW: Build CTA context
    var ctaContext = BuildCtaContext(ctx);

    var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
Thong tin da co: {contactSummary}
{vipInstruction}
{ctaContext}
Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Phan cuoi LUON LUON moi khach gui thong tin con thieu.
""";

    var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

    // No more appending - AI handles CTA naturally
    return response;
}
```

### Step 3: Remove AppendCallToAction Method (5min)

Delete method `AppendCallToAction` (lines 221-228) - không còn cần thiết.

### Step 4: Update System Prompt (10min)

Replace `{CTA_INSTRUCTION}` placeholder trong `sales-closer-system-prompt.txt`:

```
MỤC TIÊU TỐI THƯỢNG:
- Trả lời tự nhiên như người thật, ngắn gọn 2-3 câu
- DÙ ĐANG TRẢ LỜI GÌ, LUÔN LUÔN kết thúc bằng lời mời gửi thông tin còn thiếu
- Không bao giờ để cuộc trò chuyện kết thúc lửng lơ
- Mỗi câu trả lời phải hướng về việc lên đơn
- Nếu có instruction "Ket thuc:", follow đúng instruction đó
```

---

## Expected Behavior

**Before (hard-coded CTA):**
```
Bot: "Dạ kem này phù hợp da dầu luôn chị ơi."
Bot: "Chị nhắn giúp em Kem Chống Nắng, Kem Lụa hay combo 2 sản phẩm để em lên đơn nhanh nha."
```

**After (AI-generated natural CTA):**
```
Bot: "Dạ kem này phù hợp da dầu luôn chị ơi, công thức không gây bít tắc lỗ chân lông. Chị gửi em số điện thoại và địa chỉ để em lên đơn ngay nha."
```

---

## Testing

### Unit Tests (10min)

Add tests trong `ConsultingStateHandlerTests.cs`:

```csharp
[Fact]
public async Task BuildNaturalReply_HasProduct_RequestsMissingInfo()
{
    // Arrange
    _ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
    _ctx.SetData("customerPhone", null); // Missing phone

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Bao nhieu tien?");

    // Assert
    Assert.Matches(@"(so dien thoai|sdt|phone)", response.ToLower());
}

[Fact]
public async Task BuildNaturalReply_NoProduct_RequestsProductSelection()
{
    // Arrange
    _ctx.SetData("selectedProductCodes", new List<string>());

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Kem nao tot?");

    // Assert
    Assert.Matches(@"(kem chong nang|kem lua|combo)", response.ToLower());
}

[Fact]
public async Task BuildNaturalReply_AllInfoComplete_RequestsConfirmation()
{
    // Arrange
    _ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
    _ctx.SetData("customerPhone", "0901234567");
    _ctx.SetData("shippingAddress", "123 ABC Street");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Ok len don di");

    // Assert
    Assert.Matches(@"(xac nhan|len don)", response.ToLower());
}
```

---

## Success Criteria

- [x] CTA context được build dựa trên missing info
- [x] CTA instruction được pass vào AI prompt
- [x] AI generates natural CTA (không hard-coded)
- [x] `AppendCallToAction` method đã bị xóa
- [x] Response tự nhiên, không cứng nhắc
- [x] Unit tests pass

---

## Risk Assessment

**Risk:** Low - AI generates CTA based on context

**Potential Issues:**
1. AI không follow CTA instruction → Add validation check
2. CTA bị thiếu → Fallback to hard-coded nếu cần
3. CTA quá dài → Add length constraint trong prompt

**Mitigation:**
- Monitor first 50 responses for CTA compliance
- Add logging để track CTA presence
- Keep hard-coded CTA as fallback option

---

## Rollback Plan

1. Restore `AppendCallToAction` method
2. Revert prompt changes
3. Re-add hard-coded CTA logic
4. Estimated rollback time: 3 minutes

---

## Monitoring

Add validation check:

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message)
{
    // ... existing code ...

    var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

    // Validation: Check if CTA present
    var hasCtaKeywords = new[] { "gui", "len don", "dia chi", "so dien thoai" }
        .Any(keyword => response.ToLower().Contains(keyword));

    if (!hasCtaKeywords)
    {
        _logger.LogWarning(
            "Response missing CTA for {PSID}, adding fallback",
            ctx.FacebookPSID
        );

        // Fallback to hard-coded CTA
        var fallbackCta = BuildCtaContext(ctx);
        response += $" {fallbackCta}";
    }

    return response;
}
```

---

## Next Steps

After Phase 3 complete:
- Proceed to Phase 4: Update Tests
- Monitor CTA compliance rate
- Adjust prompt nếu CTA quality không đạt
