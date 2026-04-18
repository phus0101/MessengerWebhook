using System.Text.Json;
using MessengerWebhook.Models;
using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class ConversationFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConversationFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessMessage_ProductInterest_TransitionsToCollectingInfo()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-product-interest-{Guid.NewGuid()}";

        var reply = await stateMachine.ProcessMessageAsync(psid, "Toi muon mua kem chong nang", _factory.PrimaryPageId);

        reply.Should().Contain("Kem Chong Nang");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        tenantContext.TenantId.Should().Be(_factory.PrimaryTenantId);

        var session = dbContext.ConversationSessions.Single(x => x.FacebookPSID == psid);
        session.CurrentState.Should().Be(ConversationState.CollectingInfo);
        session.FacebookPageId.Should().Be(_factory.PrimaryPageId);
    }

    [Fact]
    public async Task ProcessMessage_WithPhoneAndAddress_ShouldShowFinalSummaryBeforeCreatingDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-final-summary-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "Toi muon mua kem chong nang", _factory.PrimaryPageId);
        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "So cua chi la 0901234567, dia chi 12 Tran Hung Dao quan 1",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        summaryReply.Should().ContainEquivalentOf("0901234567");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var reply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("đơn nháp");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.Complete);

        var draft = dbContext.DraftOrders.Single(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be("0901234567");
        draft.ShippingAddress.Should().Contain("12 Tran Hung Dao");
        draft.FacebookPageId.Should().Be(_factory.PrimaryPageId);
        draft.Items.Should().ContainSingle(x => x.ProductCode == "KCN");
    }

    [Fact]
    public async Task ProcessMessage_PolicyException_EscalatesToHumanHandoffAndLocksConversation()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-policy-exception-{Guid.NewGuid()}";

        var reply = await stateMachine.ProcessMessageAsync(
            psid,
            "Chi muon mien phi van chuyen va them khuyen mai nhe",
            _factory.PrimaryPageId);

        reply.Should().NotBeNullOrWhiteSpace();

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.HumanHandoff);

        var supportCase = dbContext.HumanSupportCases.Single(x => x.FacebookPSID == psid);
        supportCase.Status.Should().Be(SupportCaseStatus.Open);
        supportCase.FacebookPageId.Should().Be(_factory.PrimaryPageId);

        var botLock = dbContext.BotConversationLocks.Single(x => x.FacebookPSID == psid && x.IsLocked);
        botLock.HumanSupportCaseId.Should().Be(supportCase.Id);
        _factory.EmailSpy.Notifications.Should().ContainSingle(x => x.Id == supportCase.Id);
    }

    [Fact]
    public async Task StatePersistence_AcrossScopes_MaintainsSalesContext()
    {
        var psid = $"conversation-state-persistence-{Guid.NewGuid()}";

        using (var firstScope = await CreateStateMachineScopeAsync())
        {
            var firstStateMachine = firstScope.ServiceProvider.GetRequiredService<IStateMachine>();
            await firstStateMachine.ProcessMessageAsync(psid, "Toi muon mua kem lua", _factory.PrimaryPageId);
        }

        using var secondScope = _factory.Services.CreateScope();
        InitializeTenantContext(secondScope);
        var secondStateMachine = secondScope.ServiceProvider.GetRequiredService<IStateMachine>();
        var context = await secondStateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);

        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        (context.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Should().Contain("KL");
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_UsesRememberedContactWithSoftConfirmation()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-returning-soft-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        var reply = await stateMachine.ProcessMessageAsync(
            psid,
            "Toi muon mua kem chong nang",
            _factory.PrimaryPageId);

        reply.Should().Contain("lan truoc");
        reply.Should().Contain("len don luon");
        reply.Should().NotContain("so dien thoai va dia chi em len don luon nha.");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        context.GetData<string>("customerPhone").Should().Be(customer.PhoneNumber);
        context.GetData<string>("shippingAddress").Should().Be(customer.ShippingAddress);
        context.GetData<bool?>("contactNeedsConfirmation").Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_CanUpdateAddressWithoutReenteringPhone()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-update-address-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(
            psid,
            "Toi muon mua kem lua",
            _factory.PrimaryPageId);

        var reply = await stateMachine.ProcessMessageAsync(
            psid,
            "Dia chi moi cua chi la 45 Nguyen Trai Quan 5",
            _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("tóm tắt đơn");

        var confirmReply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);
        confirmReply.Should().ContainEquivalentOf("đơn nháp");

        var draft = dbContext.DraftOrders
            .OrderByDescending(x => x.CreatedAt)
            .First(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be(customer.PhoneNumber);
        draft.ShippingAddress.Should().Contain("45 Nguyen Trai Quan 5");

        var persistedCustomer = dbContext.CustomerIdentities.Single(x => x.FacebookPSID == psid);
        persistedCustomer.ShippingAddress.Should().Be(customer.ShippingAddress);
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_NaturalAddressWithoutExplicitPrefix_ShouldStillUpdateCurrentOrderContact()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-natural-address-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(
            psid,
            "Toi muon mua kem lua",
            _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(
            psid,
            "ok vậy chị chốt nhé",
            _factory.PrimaryPageId);

        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "45 Nguyen Trai Quan 5",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        summaryReply.Should().ContainEquivalentOf("45 Nguyen Trai Quan 5");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var draftReply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);
        draftReply.Should().ContainEquivalentOf("đơn nháp");

        var draft = dbContext.DraftOrders
            .OrderByDescending(x => x.CreatedAt)
            .First(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be(customer.PhoneNumber);
        draft.ShippingAddress.Should().Contain("45 Nguyen Trai Quan 5");
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_ReenterSameRememberedContact_DoesNotAskToSaveNewContact()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-reenter-contact-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(
            psid,
            "mặt nạ ngủ",
            _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(
            psid,
            "ok vậy chị chốt nhé",
            _factory.PrimaryPageId);

        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            $"Số của chị là {customer.PhoneNumber}, địa chỉ {customer.ShippingAddress}",
            _factory.PrimaryPageId);

        Assert.DoesNotContain("thông tin mới", summaryReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("thong tin moi", summaryReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cập nhật luôn cho các đơn sau", summaryReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cap nhat luon cho cac don sau", summaryReply, StringComparison.OrdinalIgnoreCase);
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        var draft = dbContext.DraftOrders
            .OrderByDescending(x => x.CreatedAt)
            .First(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be(customer.PhoneNumber);
        draft.ShippingAddress.Should().Contain(customer.ShippingAddress!);
    }

    [Fact]
    public async Task ProcessMessage_AskFreeshipBeforeBuying_DoesNotCreateDraftUntilExplicitBuyIntent()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-freeship-before-buy-{Guid.NewGuid()}";

        var firstReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Toi muon mua kem chong nang",
            _factory.PrimaryPageId);

        firstReply.Should().Contain("Kem Chong Nang");

        var secondReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Co freeship khong em?",
            _factory.PrimaryPageId);

        secondReply.Should().ContainEquivalentOf("chưa dám chốt freeship");
        Assert.DoesNotContain("da len don", secondReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("len don luon", secondReply, StringComparison.OrdinalIgnoreCase);
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var thirdReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Ok em len don luon, so cua chi la 0901234567, dia chi 12 Tran Hung Dao quan 1",
            _factory.PrimaryPageId);

        thirdReply.Should().ContainEquivalentOf("tóm tắt đơn");
        Assert.DoesNotContain("chị gửi em", thirdReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chi gui em", thirdReply, StringComparison.OrdinalIgnoreCase);
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var confirmReply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        confirmReply.Should().ContainEquivalentOf("đơn nháp");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMessage_OkEAfterShippingReplyWithoutContact_ShouldAskForContactInsteadOfPretendingContactExists()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-kl-shipping-{Guid.NewGuid()}";

        var firstReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Toi muon mua kem lua",
            _factory.PrimaryPageId);

        firstReply.Should().Contain("Kem Lua");

        var secondReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Co freeship khong em?",
            _factory.PrimaryPageId);

        secondReply.Should().ContainEquivalentOf("chưa dám chốt freeship");

        var thirdReply = await stateMachine.ProcessMessageAsync(
            psid,
            "ok e",
            _factory.PrimaryPageId);

        Assert.Contains("số điện thoại", thirdReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("địa chỉ", thirdReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("đã nhận được thông tin", thirdReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("da nhan duoc thong tin", thirdReply, StringComparison.OrdinalIgnoreCase);
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessMessage_WhenSelectedProductCodesLost_ReverseScanPrefersLatestUserProduct()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-reverse-scan-{Guid.NewGuid()}";

        var firstReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Chi dang xem kem chong nang",
            _factory.PrimaryPageId);
        firstReply.Should().Contain("Kem Chong Nang");

        var session = dbContext.ConversationSessions.Single(x => x.FacebookPSID == psid);
        var context = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.ContextJson!)!;
        context.Remove("selectedProductCodes");
        context["conversationHistory"] = JsonSerializer.SerializeToElement(new object[]
        {
            new { Role = "assistant", Content = "Dạ Kem Chong Nang bên em chống nắng tốt ạ.", Timestamp = DateTime.UtcNow.AddMinutes(-3) },
            new { Role = "user", Content = "Thôi chị lấy kem lua nha em", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new { Role = "assistant", Content = "Dạ em hỗ trợ tiếp cho chị nha.", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });
        session.ContextJson = JsonSerializer.Serialize(context);
        dbContext.SaveChanges();

        var finalReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Ok em len don luon, so cua chi la 0901234567, dia chi 12 Tran Hung Dao quan 1",
            _factory.PrimaryPageId);

        finalReply.Should().ContainEquivalentOf("tóm tắt đơn");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        var draft = dbContext.DraftOrders.Single(x => x.FacebookPSID == psid);
        draft.Items.Should().ContainSingle(x => x.ProductCode == "KL");
        draft.Items.Should().NotContain(x => x.ProductCode == "KCN");
    }

    [Fact]
    public async Task ProcessMessage_FirstGreeting_ReturningCustomer_AddsConsultTransition()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-first-greeting-{Guid.NewGuid()}";
        await SeedReturningCustomerAsync(dbContext, psid);

        var reply = await stateMachine.ProcessMessageAsync(psid, "hi sốp", _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("chào");
        reply.Should().ContainEquivalentOf("tư vấn");
        reply.Should().Contain("?");
    }

    [Fact]
    public async Task ProcessMessage_Turn2NeedStatement_ShouldNotRegreet()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var psid = $"conversation-greeting-turn2-{Guid.NewGuid()}";

        var firstReply = await stateMachine.ProcessMessageAsync(psid, "hi sốp", _factory.PrimaryPageId);
        firstReply.Should().ContainEquivalentOf("chào");

        var secondReply = await stateMachine.ProcessMessageAsync(
            psid,
            "chị muốn tìm sản phẩm dưỡng da vì hay đi ngoài trời nhiều",
            _factory.PrimaryPageId);

        Assert.DoesNotContain("dạ chào", secondReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chào chị", secondReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("em chào chị", secondReply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessMessage_AskFreeshipForMask_UsesCurrentPolicyInsteadOfTwoProductShortcut()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var psid = $"conversation-mn-policy-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        var reply = await stateMachine.ProcessMessageAsync(psid, "có freeship ko em", _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("chưa dám chốt freeship");
        reply.Should().ContainEquivalentOf("quà tặng");
        reply.Should().NotContainEquivalentOf("từ 2 sản phẩm");
    }

    [Fact]
    public async Task ProcessMessage_FirstTurnMaskPriceQuestion_ShouldUseRuntimePriceInsteadOfAiFallback()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-mn-first-turn-price-{Guid.NewGuid()}";
        var product = dbContext.Products.Single(x => x.TenantId == _factory.PrimaryTenantId && x.Code == "MN");
        var expectedPriceText = $"{product.BasePrice:N0}đ";

        var reply = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm giá bao nhiêu", _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("mat na ngu");
        reply.Should().ContainEquivalentOf(expectedPriceText);
        reply.Should().ContainEquivalentOf("dữ liệu nội bộ");
        reply.Should().NotContainEquivalentOf("350k");
    }

    [Fact]
    public async Task ProcessMessage_FirstTurnMaskPriceQuestionWithQuestionMark_ShouldPreferPriceReplyOverPolicyReply()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var psid = $"conversation-mn-first-turn-price-qm-{Guid.NewGuid()}";
        var product = dbContext.Products.Single(x => x.TenantId == _factory.PrimaryTenantId && x.Code == "MN");
        var expectedPriceText = $"{product.BasePrice:N0}đ";

        var reply = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm giá bao nhiêu vậy?", _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("mat na ngu");
        reply.Should().ContainEquivalentOf(expectedPriceText);
        reply.Should().ContainEquivalentOf("dữ liệu nội bộ");
        reply.Should().NotContainEquivalentOf("freeship");
    }

    [Fact]
    public async Task ProcessMessage_MaskConsultPolicyAndCheckout_ShouldKeepLockedProductAcrossRepliesAndDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-mn-locked-flow-{Guid.NewGuid()}";

        var firstReply = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm", _factory.PrimaryPageId);
        firstReply.Should().ContainEquivalentOf("mat na ngu");

        var consultReply = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ này dùng cho nam được không em?", _factory.PrimaryPageId);
        consultReply.Should().ContainEquivalentOf("mat na ngu");
        consultReply.Should().NotContainEquivalentOf("kem lua");

        var promoReply = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ có khuyến mãi gì không em, kem lụa chị chưa cần", _factory.PrimaryPageId);
        promoReply.Should().ContainEquivalentOf("mat na ngu");
        promoReply.Should().ContainEquivalentOf("quà tặng");
        promoReply.Should().NotContainEquivalentOf("kem lua");

        var shippingReply = await stateMachine.ProcessMessageAsync(psid, "có freeship không em?", _factory.PrimaryPageId);
        shippingReply.Should().ContainEquivalentOf("mat na ngu");
        shippingReply.Should().ContainEquivalentOf("quà tặng");
        shippingReply.Should().NotContainEquivalentOf("kem lua");

        var finalReply = await stateMachine.ProcessMessageAsync(
            psid,
            "ok em lên đơn luôn, số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1",
            _factory.PrimaryPageId);

        finalReply.Should().ContainEquivalentOf("tóm tắt đơn");
        finalReply.Should().ContainEquivalentOf("mat na ngu");
        finalReply.Should().NotContainEquivalentOf("kem lua");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        var draft = dbContext.DraftOrders.Single(x => x.FacebookPSID == psid);
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");
        draft.Items.Should().NotContain(x => x.ProductCode == "KL");
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomerTranscript_ShouldKeepMnFactsAndAskToConfirmRememberedContactBeforeDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"conversation-returning-transcript-{Guid.NewGuid()}";
        var product = dbContext.Products.Single(x => x.TenantId == _factory.PrimaryTenantId && x.Code == "MN");
        var giftName = (from mapping in dbContext.ProductGiftMappings
                        join gift in dbContext.Gifts on mapping.GiftCode equals gift.Code
                        where mapping.TenantId == _factory.PrimaryTenantId && mapping.ProductCode == "MN"
                        select gift.Name).Single();
        var customer = await SeedReturningCustomerAsync(dbContext, psid);
        var draftCountBefore = dbContext.DraftOrders.Count(x => x.FacebookPSID == psid);
        var shippingFee = 30000m;
        var totalText = $"{product.BasePrice + shippingFee:N0}đ";
        var priceText = $"{product.BasePrice:N0}đ";

        var greetingReply = await stateMachine.ProcessMessageAsync(psid, "hi", _factory.PrimaryPageId);
        greetingReply.Should().ContainEquivalentOf("chào");
        greetingReply.Should().ContainEquivalentOf("tư vấn");

        var needReply = await stateMachine.ProcessMessageAsync(psid, "tìm sản phẩm dưỡng da", _factory.PrimaryPageId);
        needReply.Should().NotBeNullOrWhiteSpace();

        var outdoorReply = await stateMachine.ProcessMessageAsync(psid, "do hay đi ngoài đường nhiều nên cần sản phẩm dưỡng da tốt", _factory.PrimaryPageId);
        outdoorReply.Should().NotBeNullOrWhiteSpace();

        var detailReply = await stateMachine.ProcessMessageAsync(psid, "cho biết thêm chi tiết về mặt nạ ngủ dưỡng ẩm", _factory.PrimaryPageId);
        detailReply.Should().ContainEquivalentOf("mat na ngu");
        detailReply.Should().NotContainEquivalentOf("kem lua");

        var genderReply = await stateMachine.ProcessMessageAsync(psid, "nam nữ dùng đều được phải ko", _factory.PrimaryPageId);
        genderReply.Should().ContainEquivalentOf("mat na ngu");
        genderReply.Should().NotContainEquivalentOf("kem lua");

        var priceReply = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy", _factory.PrimaryPageId);
        priceReply.Should().ContainEquivalentOf(priceText);
        priceReply.Should().ContainEquivalentOf("mat na ngu");

        var promoReply = await stateMachine.ProcessMessageAsync(psid, "có khuyến mãi gì ko e", _factory.PrimaryPageId);
        promoReply.Should().ContainEquivalentOf(giftName);
        promoReply.Should().ContainEquivalentOf("mat na ngu");

        var shippingReply = await stateMachine.ProcessMessageAsync(psid, "có freeship ko e", _factory.PrimaryPageId);
        shippingReply.Should().ContainEquivalentOf("chưa dám chốt freeship");
        shippingReply.Should().ContainEquivalentOf(giftName);
        shippingReply.Should().ContainEquivalentOf("mat na ngu");

        var totalReply = await stateMachine.ProcessMessageAsync(psid, "vậy nếu mua sản phẩm này thì đơn sẽ có tổng cộng bao nhiêu sản phẩm, tổng tiền là bao nhiêu", _factory.PrimaryPageId);
        totalReply.Should().ContainEquivalentOf(priceText);
        totalReply.Should().ContainEquivalentOf("phí ship và tổng đơn cuối em cần kiểm tra lại");
        totalReply.Should().ContainEquivalentOf(giftName);
        totalReply.Should().ContainEquivalentOf("mat na ngu");

        var buyReply = await stateMachine.ProcessMessageAsync(psid, "ok vậy lấy sản phẩm này nhé", _factory.PrimaryPageId);
        buyReply.Should().ContainEquivalentOf(customer.PhoneNumber!);
        buyReply.Should().ContainEquivalentOf(customer.ShippingAddress!);
        buyReply.Should().NotContainEquivalentOf("DR-");
        buyReply.Should().NotContainEquivalentOf("cập nhật luôn cho các đơn sau");

        var clarifyReply = await stateMachine.ProcessMessageAsync(psid, "thông tin nào?", _factory.PrimaryPageId);
        clarifyReply.Should().ContainEquivalentOf(customer.PhoneNumber!);
        clarifyReply.Should().ContainEquivalentOf(customer.ShippingAddress!);
        clarifyReply.Should().NotContainEquivalentOf("có em cập nhật giúp chị");

        dbContext.DraftOrders.Count(x => x.FacebookPSID == psid).Should().Be(draftCountBefore);
    }

    private async Task<IServiceScope> CreateStateMachineScopeAsync()
    {
        var scope = await _factory.CreateIsolatedScopeAsync();
        InitializeTenantContext(scope);
        return scope;
    }

    private async Task<CustomerIdentity> SeedReturningCustomerAsync(MessengerBotDbContext dbContext, string psid)
    {
        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = _factory.PrimaryPageId,
            PhoneNumber = "0911111111",
            FullName = "Khach Gan Lai",
            ShippingAddress = "22 Ly Tu Trong, Quan 1",
            TenantId = _factory.PrimaryTenantId,
            TotalOrders = 12,
            SuccessfulDeliveries = 11,
            FailedDeliveries = 1
        };

        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();
        return customer;
    }

    private ITenantContext InitializeTenantContext(IServiceScope scope)
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Initialize(_factory.PrimaryTenantId, _factory.PrimaryPageId, _factory.PrimaryManagerEmail);
        return tenantContext;
    }
}
