using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class SalesAcceptanceTranscriptTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SalesAcceptanceTranscriptTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Greeting_NewCustomer_ShouldNotPretendReturningCustomerFamiliarity()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"sales-acceptance-greeting-{Guid.NewGuid()}";

        var reply = await stateMachine.ProcessMessageAsync(psid, "hi sốp", _factory.PrimaryPageId);

        reply.Should().ContainEquivalentOf("chào");
        reply.Should().ContainEquivalentOf("tư vấn");
        reply.Should().Contain("?");
        reply.Should().NotContainEquivalentOf("hỗ trợ chị lại");
        reply.Should().NotContainEquivalentOf("lâu rồi mới thấy");
        reply.Should().NotContainEquivalentOf("từ lần trước");
    }

    [Fact]
    public async Task ReturningCustomer_UpdatedContactForCurrentOrder_ShouldRequireFinalSummaryConfirmBeforeDraftAndAskToSaveForFuture()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-returning-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        var contactReply = await stateMachine.ProcessMessageAsync(psid, "ok vậy chị chốt nhé", _factory.PrimaryPageId);

        contactReply.Should().Contain(customer.PhoneNumber!);
        contactReply.Should().ContainEquivalentOf(customer.ShippingAddress!);
        contactReply.Should().NotContainEquivalentOf("đơn nháp");

        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Số mới của chị là 0988888888, địa chỉ mới là 99 Lê Lợi quận 3",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        summaryReply.Should().Contain("0988888888");
        summaryReply.Should().ContainEquivalentOf("99 Lê Lợi quận 3");
        summaryReply.Should().NotContainEquivalentOf("đã lên đơn nháp");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var draftReply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        draftReply.Should().ContainEquivalentOf("đơn nháp");
        Assert.True(
            draftReply.Contains("cập nhật", StringComparison.OrdinalIgnoreCase) ||
            draftReply.Contains("cap nhat", StringComparison.OrdinalIgnoreCase));

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.Complete);
        context.GetData<string>("pendingContactQuestion").Should().Be("ask_save_new_contact");
        context.GetData<bool?>("currentOrderUsesUpdatedContact").Should().BeTrue();
        context.GetData<bool?>("saveCurrentContactForFuture").Should().NotBeTrue();
        context.GetData<string>("customerPhone").Should().Be("0988888888");
        context.GetData<string>("shippingAddress").Should().Contain("99 Lê Lợi quận 3");

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(x => x.FacebookPSID == psid);

        draft.CustomerPhone.Should().Be("0988888888");
        draft.ShippingAddress.Should().Contain("99 Lê Lợi quận 3");
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");

        var persistedCustomer = await dbContext.CustomerIdentities.SingleAsync(x => x.FacebookPSID == psid);
        persistedCustomer.PhoneNumber.Should().Be(customer.PhoneNumber);
        persistedCustomer.ShippingAddress.Should().Be(customer.ShippingAddress);
    }

    [Fact]
    public async Task ReturningCustomer_DeclineSavingUpdatedContact_ShouldKeepOldIdentityAndClearPendingSaveQuestion()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-decline-save-{Guid.NewGuid()}";
        var customer = await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(psid, "ok vậy chị chốt nhé", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(
            psid,
            "Số mới của chị là 0988888888, địa chỉ mới là 99 Lê Lợi quận 3",
            _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        var declineReply = await stateMachine.ProcessMessageAsync(
            psid,
            "không em cứ giữ thông tin cũ",
            _factory.PrimaryPageId);

        declineReply.Should().ContainEquivalentOf("giữ nguyên thông tin cũ");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.GetData<string>("pendingContactQuestion").Should().BeNull();
        context.GetData<bool?>("currentOrderUsesUpdatedContact").Should().BeFalse();
        context.GetData<bool?>("saveCurrentContactForFuture").Should().BeFalse();

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be("0988888888");
        draft.ShippingAddress.Should().Contain("99 Lê Lợi quận 3");
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");

        var persistedCustomer = await dbContext.CustomerIdentities.SingleAsync(x => x.FacebookPSID == psid);
        persistedCustomer.PhoneNumber.Should().Be(customer.PhoneNumber);
        persistedCustomer.ShippingAddress.Should().Be(customer.ShippingAddress);
    }

    [Fact]
    public async Task ReturningCustomer_SaveUpdatedContactForFuture_ShouldPersistIdentityAndClearPendingSaveQuestion()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-save-contact-{Guid.NewGuid()}";
        await SeedReturningCustomerAsync(dbContext, psid);

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(psid, "ok vậy chị chốt nhé", _factory.PrimaryPageId);
        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "Số mới của chị là 0977777777, địa chỉ mới là 55 Hai Bà Trưng quận 3",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var draftReply = await stateMachine.ProcessMessageAsync(
            psid,
            "đúng rồi",
            _factory.PrimaryPageId);

        draftReply.Should().ContainEquivalentOf("đơn nháp");
        draftReply.Should().ContainEquivalentOf("cập nhật");

        var saveAckReply = await stateMachine.ProcessMessageAsync(
            psid,
            "có em cập nhật giúp chị",
            _factory.PrimaryPageId);

        Assert.True(
            saveAckReply.Contains("cập nhật", StringComparison.OrdinalIgnoreCase) ||
            saveAckReply.Contains("cap nhat", StringComparison.OrdinalIgnoreCase));

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.GetData<string>("pendingContactQuestion").Should().BeNull();
        context.GetData<bool?>("currentOrderUsesUpdatedContact").Should().BeFalse();
        context.GetData<bool?>("saveCurrentContactForFuture").Should().BeTrue();

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(x => x.FacebookPSID == psid);
        draft.CustomerPhone.Should().Be("0977777777");
        draft.ShippingAddress.Should().Contain("55 Hai Bà Trưng quận 3");
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");

        var persistedCustomer = await dbContext.CustomerIdentities.SingleAsync(x => x.FacebookPSID == psid);
        persistedCustomer.PhoneNumber.Should().Be("0977777777");
        persistedCustomer.ShippingAddress.Should().Contain("55 Hai Bà Trưng quận 3");
    }

    [Fact]
    public async Task MaskQuantityTranscript_ShouldPersistContextQuantityAndDraftQuantity()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-quantity-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        var quantityReply = await stateMachine.ProcessMessageAsync(psid, "mua 2 sản phẩm mặt nạ ngủ thì sao em", _factory.PrimaryPageId);

        quantityReply.Should().ContainEquivalentOf("mat na ngu");

        var pendingContext = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        (pendingContext.GetData<List<string>>("selectedProductCodes") ?? new List<string>())
            .Should().ContainSingle().Which.Should().Be("MN");

        var quantities = pendingContext.GetData<Dictionary<string, int>>("selectedProductQuantities");
        quantities.Should().NotBeNull();
        quantities!["MN"].Should().Be(2);

        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "ok em lên đơn luôn, số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        summaryReply.Should().ContainEquivalentOf("x2");
        summaryReply.Should().ContainEquivalentOf("12 Trần Hưng Đạo quận 1");
        summaryReply.Should().NotContainEquivalentOf("ok em lên đơn luôn");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        await stateMachine.ProcessMessageAsync(psid, "đúng rồi", _factory.PrimaryPageId);

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .SingleAsync(x => x.FacebookPSID == psid);

        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN" && x.Quantity == 2);
    }

    [Fact]
    public async Task AmbiguousProductReference_ShouldClarifyInsteadOfAutoPickingDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-ambiguous-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "chị đang xem kem chống nắng", _factory.PrimaryPageId);
        await stateMachine.ProcessMessageAsync(psid, "chị cũng đang cân nhắc mặt nạ ngủ", _factory.PrimaryPageId);

        var clarifyReply = await stateMachine.ProcessMessageAsync(psid, "lấy sản phẩm đó nhé", _factory.PrimaryPageId);

        clarifyReply.Should().ContainEquivalentOf("xác nhận");
        clarifyReply.Should().ContainEquivalentOf("kem chong nang");
        clarifyReply.Should().ContainEquivalentOf("mat na ngu");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();
    }

    [Fact]
    public async Task FinalSummaryFollowUpQuestion_ShouldResendSummaryAndNotCreateDraftYet()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"sales-acceptance-final-summary-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", _factory.PrimaryPageId);
        var summaryReply = await stateMachine.ProcessMessageAsync(
            psid,
            "ok em lên đơn luôn, số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1",
            _factory.PrimaryPageId);

        summaryReply.Should().ContainEquivalentOf("tóm tắt đơn");
        summaryReply.Should().ContainEquivalentOf("tổng đơn cuối");
        summaryReply.Should().ContainEquivalentOf("SĐT nhận hàng");
        summaryReply.Should().ContainEquivalentOf("địa chỉ giao hàng");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var clarifyReply = await stateMachine.ProcessMessageAsync(psid, "thông tin nào?", _factory.PrimaryPageId);
        clarifyReply.Should().ContainEquivalentOf("tóm tắt đơn");
        clarifyReply.Should().ContainEquivalentOf("0901234567");
        clarifyReply.Should().ContainEquivalentOf("12 Trần Hưng Đạo");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();
    }

    [Fact]
    public async Task PriceAndShippingQuestions_ShouldUseSafePhrasingAndSetGroundingFlagsConservatively()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"sales-acceptance-grounding-{Guid.NewGuid()}";

        await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", _factory.PrimaryPageId);

        var priceReply = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy", _factory.PrimaryPageId);
        priceReply.Should().ContainEquivalentOf("giá");
        priceReply.Should().ContainEquivalentOf("quà tặng đang gắn theo dữ liệu nội bộ hiện tại");
        priceReply.Should().ContainEquivalentOf("ưu đãi khác thì em cần kiểm tra lại");

        var shippingReply = await stateMachine.ProcessMessageAsync(psid, "có freeship không em", _factory.PrimaryPageId);
        shippingReply.Should().ContainEquivalentOf("chưa dám chốt freeship");
        shippingReply.Should().ContainEquivalentOf("quà tặng");

        var context = await stateMachine.LoadOrCreateAsync(psid, _factory.PrimaryPageId);
        context.GetData<bool?>("price_confirmed").Should().BeTrue();
        context.GetData<bool?>("promotion_confirmed").Should().BeFalse();
        context.GetData<bool?>("shipping_policy_confirmed").Should().BeFalse();
        context.GetData<bool?>("inventory_confirmed").Should().BeFalse();
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
