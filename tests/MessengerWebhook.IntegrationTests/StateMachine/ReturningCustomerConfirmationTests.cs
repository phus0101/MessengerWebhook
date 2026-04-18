using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class ReturningCustomerConfirmationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReturningCustomerConfirmationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("ok e")]
    [InlineData("chốt nhé")]
    [InlineData("đặt luôn")]
    [InlineData("lên đơn")]
    public async Task ReturningCustomer_GenericBuyContinuation_AfterRememberedContactPrompt_ShouldNotAutoConfirmOldContact(string followUpMessage)
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0987654321",
            ShippingAddress = "456 Another Street, District 3, HCMC",
            TenantId = _factory.PrimaryTenantId
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        await stateMachine.ProcessMessageAsync(psid, "cho em kem lua", pageId);

        var response1 = await stateMachine.ProcessMessageAsync(psid, "len don cho chi", pageId);
        Assert.Contains("0987654321", response1);
        Assert.Contains("456 Another Street", response1, StringComparison.OrdinalIgnoreCase);

        var response2 = await stateMachine.ProcessMessageAsync(psid, followUpMessage, pageId);

        Assert.Contains("0987654321", response2);
        Assert.Contains("456 Another Street", response2, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("đơn nháp", response2, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("don nhap", response2, StringComparison.OrdinalIgnoreCase);

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_DirectExplicitConfirm_AfterRememberedContactPrompt_ShouldCreateDraftOrder()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-direct-confirm-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = "123 Test Street, District 1, HCMC",
            TenantId = _factory.PrimaryTenantId
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        var productReply = await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);
        Assert.Contains("kem chong nang", productReply.ToLower());

        var contactReply = await stateMachine.ProcessMessageAsync(psid, "len don cho chi", pageId);
        Assert.Contains("0912345678", contactReply);
        Assert.Contains("123 Test Street", contactReply, StringComparison.OrdinalIgnoreCase);

        var confirmReply = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        Assert.Contains("tóm tắt", confirmReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(customer.PhoneNumber, confirmReply);
        Assert.Contains("123 Test Street", confirmReply, StringComparison.OrdinalIgnoreCase);

        var draftOrderBeforeFinalConfirm = await dbContext.DraftOrders
            .Include(d => d.Items)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrderBeforeFinalConfirm);

        var draftReply = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        Assert.True(
            draftReply.Contains("lên đơn", StringComparison.OrdinalIgnoreCase) ||
            draftReply.Contains("len don", StringComparison.OrdinalIgnoreCase) ||
            draftReply.Contains("đơn nháp", StringComparison.OrdinalIgnoreCase) ||
            draftReply.Contains("don nhap", StringComparison.OrdinalIgnoreCase));

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        Assert.Equal(ConversationState.Complete, context.CurrentState);
        Assert.False(context.GetData<bool?>("contactNeedsConfirmation") ?? false);
        Assert.Null(context.GetData<string>("pendingContactQuestion"));

        var draftOrder = await dbContext.DraftOrders
            .Include(d => d.Items)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.NotNull(draftOrder);
        var persistedDraftOrder = draftOrder!;
        Assert.Equal(customer.PhoneNumber, persistedDraftOrder.CustomerPhone);
        Assert.Equal(customer.ShippingAddress, persistedDraftOrder.ShippingAddress);
        Assert.Contains(persistedDraftOrder.Items, x => x.ProductCode == "KCN");
    }

    [Fact]
    public async Task ReturningCustomer_AsksIfShopHasHerInfo_ShouldKeepCurrentProductContextAndAskToConfirm()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0911222333",
            ShippingAddress = "12 Tran Hung Dao, Quan 1",
            TenantId = _factory.PrimaryTenantId
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        var response1 = await stateMachine.ProcessMessageAsync(psid, "cho em kem lua", pageId);
        Assert.Contains("kem lua", response1.ToLower());

        var response2 = await stateMachine.ProcessMessageAsync(psid, "em co thong tin cua chi chua?", pageId);

        Assert.Contains("kem lua", response2.ToLower());
        Assert.DoesNotContain("quan tam san pham nao", response2.ToLower());
        Assert.Contains("0911222333", response2);
        Assert.Contains("12 Tran Hung Dao", response2, StringComparison.OrdinalIgnoreCase);

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_GenericBuyContinuation_WithRememberedPhoneOnly_ShouldAskToConfirmPhoneAndProvideAddress()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = null,
            TenantId = _factory.PrimaryTenantId,
            TotalOrders = 1
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);
        var contactReply = await stateMachine.ProcessMessageAsync(psid, "lên đơn cho chị", pageId);
        Assert.Contains("0912345678", contactReply);
        Assert.Contains("địa chỉ", contactReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", contactReply, StringComparison.OrdinalIgnoreCase);

        var followUpReply = await stateMachine.ProcessMessageAsync(psid, "ok", pageId);
        Assert.Contains("0912345678", followUpReply);
        Assert.Contains("địa chỉ", followUpReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", followUpReply, StringComparison.OrdinalIgnoreCase);

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_DoesNotConfirm_ShouldNotCreateDraftOrder()
    {
        // Arrange
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = "123 Test Street, District 1, HCMC",
            TenantId = _factory.PrimaryTenantId
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        // Act - Step 1: Customer asks about product
        await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);

        // Act - Step 2: Customer asks unrelated question (not confirming)
        await stateMachine.ProcessMessageAsync(psid, "kem nay co tot khong?", pageId);

        // Assert - Should not create draft order yet
        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_AsksThongTinNao_AfterContactPrompt_ShouldClarifyRememberedContact()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0912345678",
            ShippingAddress = "123 Test Street, District 1, HCMC",
            TenantId = _factory.PrimaryTenantId
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);
        var contactReply = await stateMachine.ProcessMessageAsync(psid, "lên đơn cho chị", pageId);
        Assert.Contains("0912345678", contactReply);
        Assert.Contains("123 Test Street", contactReply, StringComparison.OrdinalIgnoreCase);

        var clarifyReply = await stateMachine.ProcessMessageAsync(psid, "thông tin nào?", pageId);
        Assert.Contains("0912345678", clarifyReply);
        Assert.Contains("123 Test Street", clarifyReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("xác nhận", clarifyReply, StringComparison.OrdinalIgnoreCase);

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.CustomerPhone == customer.PhoneNumber);
        Assert.Null(draftOrder);
    }

    [Fact]
    public async Task ReturningCustomer_GenericBuyContinuation_WithRememberedAddressOnly_ShouldAskToConfirmAddressAndProvidePhone()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"test-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var customer = new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = null,
            ShippingAddress = "456 Nguyen Hue, District 1, HCMC",
            TenantId = _factory.PrimaryTenantId,
            TotalOrders = 1
        };
        dbContext.CustomerIdentities.Add(customer);
        await dbContext.SaveChangesAsync();

        await stateMachine.ProcessMessageAsync(psid, "cho em xin kem chong nang", pageId);
        var contactReply = await stateMachine.ProcessMessageAsync(psid, "lên đơn cho chị", pageId);
        Assert.Contains("456 Nguyen Hue", contactReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("số điện thoại", contactReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", contactReply, StringComparison.OrdinalIgnoreCase);

        var followUpReply = await stateMachine.ProcessMessageAsync(psid, "ok", pageId);
        Assert.Contains("456 Nguyen Hue", followUpReply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("số điện thoại", followUpReply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", followUpReply, StringComparison.OrdinalIgnoreCase);

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));

        var draftOrder = await dbContext.DraftOrders
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(d => d.ShippingAddress == customer.ShippingAddress);
        Assert.Null(draftOrder);
    }

    private async Task<IServiceScope> CreateStateMachineScopeAsync()
    {
        var scope = await _factory.CreateIsolatedScopeAsync();
        InitializeTenantContext(scope);
        return scope;
    }

    private ITenantContext InitializeTenantContext(IServiceScope scope)
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Initialize(_factory.PrimaryTenantId, _factory.PrimaryPageId, _factory.PrimaryManagerEmail);
        return tenantContext;
    }
}
