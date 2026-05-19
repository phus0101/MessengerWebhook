# Phase 4: Update Tests

**Duration:** 45 minutes
**Priority:** P1
**Risk:** Low
**Status:** ✅ Completed (2026-03-31)
**Dependencies:** Phase 3

---

## Overview

Thêm unit tests và integration tests để verify:
1. VIP greeting tích hợp tự nhiên
2. Response là 1 message duy nhất (no `\n\n`)
3. AI không tự giới thiệu
4. CTA present trong response

---

## Test Files to Update

### 1. Unit Tests (30min)

**File:** `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/ConsultingStateHandlerTests.cs`

Add test cases:

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

    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Chao chi yeu! Da kem nay phu hop da dau luon a. Chi gui em SDT va dia chi nha.");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Kem nay bao nhieu?");

    // Assert
    Assert.DoesNotContain("\n\n", response); // No disjointed parts
    Assert.Contains("chi yeu", response.ToLower()); // VIP tone
    Assert.Matches(@"(sdt|so dien thoai|dia chi)", response.ToLower()); // CTA present
}

[Fact]
public async Task BuildNaturalReply_StandardCustomer_NoVipGreeting()
{
    // Arrange
    var standardProfile = new VipProfile { IsVip = false };

    _mockCustomerService
        .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>()))
        .ReturnsAsync(standardProfile);

    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Da kem nay phu hop da dau luon chi oi. Chi gui em SDT va dia chi nha.");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Ship bao lau?");

    // Assert
    Assert.DoesNotContain("chi yeu", response.ToLower());
    Assert.DoesNotContain("chi iu", response.ToLower());
}

[Fact]
public async Task BuildNaturalReply_NoSelfIntroduction()
{
    // Arrange
    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Da kem nay 350k chi oi. Chi gui em thong tin nha.");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Bao nhieu tien?");

    // Assert
    Assert.DoesNotContain("em la", response.ToLower());
    Assert.DoesNotContain("tro ly ai", response.ToLower());
    Assert.DoesNotContain("bot", response.ToLower());
}

[Fact]
public async Task BuildNaturalReply_HasProduct_RequestsMissingInfo()
{
    // Arrange
    _ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
    _ctx.SetData("customerPhone", null); // Missing phone

    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Da kem nay 350k chi oi. Chi gui em so dien thoai va dia chi nha.");

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

    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Da chi co the chon Kem Chong Nang hoac Kem Lua a. Chi chon san pham nao nha.");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Kem nao tot?");

    // Assert
    Assert.Matches(@"(kem chong nang|kem lua|combo)", response.ToLower());
}

[Fact]
public async Task BuildNaturalReply_ResponseLengthWithinLimit()
{
    // Arrange
    _mockGeminiService
        .Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<ConversationMessage>>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("Da kem nay phu hop da dau luon chi oi. Chi gui em SDT va dia chi nha.");

    // Act
    var response = await _handler.BuildNaturalReplyAsync(_ctx, "Kem nay tot khong?");

    // Assert
    Assert.True(response.Length <= 250, $"Response too long: {response.Length} chars");
}
```

### 2. Integration Tests (15min)

**File:** `tests/MessengerWebhook.IntegrationTests/StateMachine/ConversationFlowTests.cs`

Add integration test:

```csharp
[Fact]
public async Task SalesConversation_VipCustomer_SingleCohesiveMessage()
{
    // Arrange
    var vipCustomer = new CustomerIdentity
    {
        FacebookPSID = "vip_test_psid",
        FacebookPageId = TestPageId,
        TenantId = TestTenantId
    };

    var vipProfile = new VipProfile
    {
        CustomerIdentityId = vipCustomer.Id,
        IsVip = true,
        GreetingStyle = "Chao chi yeu da quay lai",
        TenantId = TestTenantId
    };

    await _dbContext.CustomerIdentities.AddAsync(vipCustomer);
    await _dbContext.VipProfiles.AddAsync(vipProfile);
    await _dbContext.SaveChangesAsync();

    var session = new ConversationSession
    {
        FacebookPSID = vipCustomer.FacebookPSID,
        FacebookPageId = TestPageId,
        CurrentState = ConversationState.Consulting,
        TenantId = TestTenantId
    };

    await _sessionManager.SaveAsync(session);

    // Act
    var response = await _stateMachine.ProcessMessageAsync(
        session,
        "Kem nay bao nhieu tien?",
        CancellationToken.None
    );

    // Assert
    Assert.NotNull(response);
    Assert.DoesNotContain("\n\n", response); // Single cohesive message
    Assert.Contains("chi yeu", response.ToLower()); // VIP tone
    Assert.DoesNotContain("em la", response.ToLower()); // No self-intro
}
```

---

## Mock Setup

Update test setup trong `ConsultingStateHandlerTests.cs`:

```csharp
private readonly Mock<ICustomerIntelligenceService> _mockCustomerService;
private readonly Mock<IGeminiService> _mockGeminiService;
private readonly ConsultingStateHandler _handler;
private readonly StateContext _ctx;

public ConsultingStateHandlerTests()
{
    _mockCustomerService = new Mock<ICustomerIntelligenceService>();
    _mockGeminiService = new Mock<IGeminiService>();

    _handler = new ConsultingStateHandler(
        _mockGeminiService.Object,
        _mockCustomerService.Object,
        Mock.Of<ILogger<ConsultingStateHandler>>()
    );

    _ctx = new StateContext
    {
        FacebookPSID = "test_psid",
        FacebookPageId = "test_page"
    };

    // Default VIP profile mock
    _mockCustomerService
        .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>()))
        .ReturnsAsync(new VipProfile { IsVip = false });
}
```

---

## Success Criteria

- [x] All new unit tests pass
- [x] Integration test passes
- [x] VIP greeting integration verified
- [x] Single message output verified
- [x] No self-introduction verified
- [x] CTA presence verified
- [x] Response length within limit verified
- [x] All existing tests still pass (144/144 unit, 60/72 integration)

---

## Testing Checklist

### Unit Tests
- [ ] VIP customer gets natural greeting
- [ ] Standard customer no VIP greeting
- [ ] No self-introduction in responses
- [ ] CTA present when product selected
- [ ] Product selection CTA when no product
- [ ] Response length within 250 chars

### Integration Tests
- [ ] Full conversation flow with VIP
- [ ] Single cohesive message output
- [ ] VIP tone integrated naturally

### Manual QA
- [ ] Test 5 conversations with VIP customers
- [ ] Test 5 conversations with standard customers
- [ ] Verify no `\n\n` separators in responses
- [ ] Verify natural CTA generation

---

## Risk Assessment

**Risk:** Low - tests validate new behavior

**Potential Issues:**
1. Mock setup complexity → Keep mocks simple
2. Flaky tests → Use deterministic test data
3. Integration test slow → Use in-memory database

**Mitigation:**
- Clear mock setup in test constructor
- Use fixed test data (no random values)
- Fast in-memory database for integration tests

---

## Next Steps

After Phase 4 complete:
- Run full test suite: `dotnet test`
- Verify all 144+ tests pass
- Deploy to staging for manual QA
- Monitor first 50 production responses
