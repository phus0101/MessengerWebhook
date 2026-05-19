---
title: "Fix Chat Response Quality Issues"
description: "Consolidate 3 disjointed messages into 1 cohesive AI-generated response"
status: pending
priority: P1
effort: 3h
branch: master
tags: [ai, sales-bot, quality, gemini]
created: 2026-03-31
---

# Implementation Plan: Fix Chat Response Quality Issues

## Problem Statement

Bot sends 3 disjointed messages instead of 1 cohesive response:
1. VIP greeting (separate message)
2. AI response (doesn't follow system prompt, introduces self)
3. Hard-coded CTA (not natural)

## Root Cause Analysis

**File:** `SalesStateHandlerBase.cs:172-195`

```csharp
private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message)
{
    // Line 188: AI generates response WITHOUT VIP context
    var response = await GeminiService.SendMessageAsync(ctx.FacebookPSID, prompt, history);

    // Line 189: Gets VIP greeting separately
    var vipGreeting = await GetVipGreetingAsync(ctx);

    // Line 190-192: Hard-coded CTA
    var cta = HasSelectedProduct(ctx)
        ? SalesMessageParser.BuildMissingInfoPrompt(ctx)
        : "Chi nhan giup em Kem Chong Nang, Kem Lua hay combo 2 san pham de em len don nhanh nha.";

    // Line 193: Appends CTA
    var reply = AppendCallToAction(response, cta);

    // Line 194: Prepends VIP greeting
    return string.IsNullOrWhiteSpace(vipGreeting) ? reply : $"{vipGreeting}\n\n{reply}";
}
```

**Issues:**
- VIP greeting prepended AFTER AI generates response
- AI doesn't know customer is VIP
- Hard-coded CTA appended mechanically
- System prompt lacks strong instructions to prevent self-introduction

## Solution Architecture

### Data Flow (Before → After)

**Before:**
```
User Message → AI (no VIP context) → Response
                                    ↓
                            VIP Greeting (prepend)
                                    ↓
                            Hard-coded CTA (append)
                                    ↓
                            3 disjointed parts
```

**After:**
```
User Message → Get VIP Profile → Build Enhanced Prompt → AI → 1 cohesive response
                                 (includes VIP context,
                                  CTA instructions)
```

## Implementation Phases

### Phase 1: Enhance System Prompt (30min)

**File:** `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`

**Changes:**
1. Add anti-self-introduction rule at top
2. Add VIP context placeholder
3. Strengthen CTA instruction
4. Add response length constraint

**New Rules Section:**
```
QUY TẮC BẮT BUỘC (THÊM):
- KHÔNG BAO GIỜ tự giới thiệu bản thân (VD: "Em là trợ lý AI", "Em là bot")
- Trả lời NGAY VÀO VẤN ĐỀ, không mở đầu bằng lời chào dài
- Tối đa 2-3 câu, ngắn gọn, tự nhiên
- Nếu khách là VIP: {VIP_GREETING_INSTRUCTION}
```

**Risk:** Low - additive change, doesn't break existing behavior

### Phase 2: Integrate VIP Context into AI Prompt (1h)

**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

**Changes:**

1. **Move VIP lookup BEFORE AI call** (line 174)
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
    return response; // No more prepending/appending
}
```

2. **Add helper method**
```csharp
private static string BuildVipInstruction(VipProfile vipProfile)
{
    if (!vipProfile.IsVip || string.IsNullOrWhiteSpace(vipProfile.GreetingStyle))
        return string.Empty;

    return $"""
Khach hang VIP:
- Dung giong dieu than mat, gan gui hon (VD: "chi iu", "chi yeu")
- Mo dau bang: "{vipProfile.GreetingStyle}"
- KHONG doi chinh sach gia, chi doi giong dieu
""";
}
```

3. **Refactor GetVipProfileAsync** (line 197)
```csharp
private async Task<VipProfile> GetVipProfileAsync(StateContext ctx)
{
    var customer = await CustomerIntelligenceService.GetExistingAsync(
        ctx.FacebookPSID,
        ctx.GetData<string>("facebookPageId"))
        ?? await CustomerIntelligenceService.GetOrCreateAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"),
            ctx.GetData<string>("customerPhone"));

    return await CustomerIntelligenceService.GetVipProfileAsync(customer);
}
```

**Risk:** Medium - changes core response generation logic
**Mitigation:** Keep old method as `BuildNaturalReplyAsync_Legacy` for rollback

### Phase 3: Remove Hard-coded CTA Logic (45min)

**File:** `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`

**Changes:**

1. **Update prompt to include CTA context** (line 177-186)
```csharp
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
```

2. **Add CTA context builder**
```csharp
private static string BuildCtaContext(StateContext ctx)
{
    var hasProduct = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
    var missingInfo = GetMissingContactInfo(ctx);

    if (missingInfo.Count == 0)
        return "Ket thuc: Moi khach xac nhan thong tin de len don.";

    if (hasProduct)
    {
        var missing = string.Join(" va ", missingInfo);
        return $"Ket thuc: Moi khach gui {missing} de len don.";
    }

    return "Ket thuc: Moi khach chon san pham (Kem Chong Nang, Kem Lua, hoac combo) va gui thong tin de len don.";
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

3. **Remove AppendCallToAction method** (line 221-228)

**Risk:** Low - CTA generation moves to AI, more natural
**Mitigation:** Monitor first 50 responses for CTA compliance

### Phase 4: Update Tests (45min)

**Files:**
- `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/ConsultingStateHandlerTests.cs`
- `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

**Changes:**
1. Mock `GetVipProfileAsync` to return VIP/non-VIP profiles
2. Assert response is single message (no `\n\n` separators)
3. Assert VIP greeting integrated naturally
4. Assert CTA present in response

**New Test Cases:**
```csharp
[Fact]
public async Task BuildNaturalReply_VipCustomer_IntegratesGreetingNaturally()
{
    // Arrange: VIP customer with greeting
    var vipProfile = new VipProfile
    {
        IsVip = true,
        GreetingStyle = "Chao chi yeu da quay lai voi Mui Xu"
    };

    // Act
    var response = await handler.BuildNaturalReplyAsync(ctx, "Kem nay bao nhieu tien?");

    // Assert
    Assert.DoesNotContain("\n\n", response); // No disjointed parts
    Assert.Contains("chi yeu", response); // VIP tone
    Assert.Matches(@"(so dien thoai|dia chi|len don)", response); // CTA present
}

[Fact]
public async Task BuildNaturalReply_StandardCustomer_NoVipGreeting()
{
    // Arrange: Standard customer
    var vipProfile = new VipProfile { IsVip = false };

    // Act
    var response = await handler.BuildNaturalReplyAsync(ctx, "Ship bao lau?");

    // Assert
    Assert.DoesNotContain("chi yeu", response);
    Assert.DoesNotContain("chi iu", response);
}
```

**Risk:** Low - tests validate new behavior

## Success Criteria

- [ ] Bot sends 1 message instead of 3
- [ ] VIP greeting integrated naturally (not prepended)
- [ ] AI doesn't introduce self ("Em là trợ lý AI")
- [ ] CTA generated naturally by AI (not hard-coded)
- [ ] Response length: 2-3 sentences max
- [ ] All existing tests pass
- [ ] New tests cover VIP/non-VIP scenarios

## Rollback Plan

1. Revert `SalesStateHandlerBase.cs` to commit `769335f`
2. Revert system prompt to previous version
3. Deploy previous build
4. Estimated rollback time: 5 minutes

## Testing Strategy

### Unit Tests
- Mock VIP profile service
- Test VIP vs non-VIP response generation
- Test CTA context building

### Integration Tests
- Full conversation flow with VIP customer
- Full conversation flow with standard customer
- Verify single message output

### Manual QA
- Test 10 conversations with VIP customers
- Test 10 conversations with standard customers
- Verify no self-introduction
- Verify natural CTA

## Dependencies

- `CustomerIntelligenceService.GetVipProfileAsync` must return valid `VipProfile`
- `GeminiService.SendMessageAsync` must respect enhanced system prompt
- No breaking changes to `StateContext` data structure

## Migration Path

No data migration required - changes are code-only.

## Backwards Compatibility

✅ Fully compatible - no API changes, no database schema changes

## Performance Impact

- **Before:** 1 VIP lookup AFTER AI call (wasted)
- **After:** 1 VIP lookup BEFORE AI call (used)
- **Net change:** 0 additional DB queries

## Security Considerations

- VIP status not exposed to customer
- No PII in system prompt
- CTA instructions don't leak internal logic

## Monitoring & Observability

Add logging:
```csharp
_logger.LogInformation(
    "Generated response for {PSID}, VIP: {IsVip}, Length: {Length}",
    ctx.FacebookPSID,
    vipProfile.IsVip,
    response.Length
);
```

Track metrics:
- Average response length (target: <200 chars)
- VIP greeting integration rate (target: 100%)
- Self-introduction incidents (target: 0)

## Unresolved Questions

1. Should we A/B test new vs old approach?
2. What's acceptable response length variance? (currently 2-3 sentences)
3. Should CTA be mandatory or optional based on conversation context?

---

**Status:** DONE
**Summary:** Comprehensive plan created with 4 phases, risk assessment, rollback strategy, and success criteria.
**Concerns:** None - plan is actionable and low-risk.
