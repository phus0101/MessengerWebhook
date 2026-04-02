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
        _factory.ResetStateAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ProcessMessage_ProductInterest_TransitionsToCollectingInfo()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantContext = InitializeTenantContext(scope);
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var reply = await stateMachine.ProcessMessageAsync("psid-conversation-1", "Toi muon mua kem chong nang", _factory.PrimaryPageId);

        reply.Should().Contain("Kem Chong Nang");

        var context = await stateMachine.LoadOrCreateAsync("psid-conversation-1", _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        tenantContext.TenantId.Should().Be(_factory.PrimaryTenantId);

        var session = dbContext.ConversationSessions.Single(x => x.FacebookPSID == "psid-conversation-1");
        session.CurrentState.Should().Be(ConversationState.CollectingInfo);
        session.FacebookPageId.Should().Be(_factory.PrimaryPageId);
    }

    [Fact]
    public async Task ProcessMessage_WithPhoneAndAddress_CreatesDraftAndCompletes()
    {
        using var scope = _factory.Services.CreateScope();
        InitializeTenantContext(scope);
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        await stateMachine.ProcessMessageAsync("psid-conversation-2", "Toi muon mua kem chong nang", _factory.PrimaryPageId);
        var reply = await stateMachine.ProcessMessageAsync(
            "psid-conversation-2",
            "So cua chi la 0901234567, dia chi 12 Tran Hung Dao quan 1",
            _factory.PrimaryPageId);

        reply.Should().Contain("don nhap");

        var context = await stateMachine.LoadOrCreateAsync("psid-conversation-2", _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.Complete);

        var draft = dbContext.DraftOrders.Single(x => x.FacebookPSID == "psid-conversation-2");
        draft.CustomerPhone.Should().Be("0901234567");
        draft.ShippingAddress.Should().Contain("12 Tran Hung Dao");
        draft.FacebookPageId.Should().Be(_factory.PrimaryPageId);
        draft.Items.Should().ContainSingle(x => x.ProductCode == "KCN");
    }

    [Fact]
    public async Task ProcessMessage_PolicyException_EscalatesToHumanHandoffAndLocksConversation()
    {
        using var scope = _factory.Services.CreateScope();
        InitializeTenantContext(scope);
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var reply = await stateMachine.ProcessMessageAsync(
            "psid-conversation-3",
            "Chi muon mien phi van chuyen va them khuyen mai nhe",
            _factory.PrimaryPageId);

        reply.Should().NotBeNullOrWhiteSpace();

        var context = await stateMachine.LoadOrCreateAsync("psid-conversation-3", _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.HumanHandoff);

        var supportCase = dbContext.HumanSupportCases.Single(x => x.FacebookPSID == "psid-conversation-3");
        supportCase.Status.Should().Be(SupportCaseStatus.Open);
        supportCase.FacebookPageId.Should().Be(_factory.PrimaryPageId);

        var botLock = dbContext.BotConversationLocks.Single(x => x.FacebookPSID == "psid-conversation-3" && x.IsLocked);
        botLock.HumanSupportCaseId.Should().Be(supportCase.Id);
        _factory.EmailSpy.Notifications.Should().ContainSingle(x => x.Id == supportCase.Id);
    }

    [Fact]
    public async Task StatePersistence_AcrossScopes_MaintainsSalesContext()
    {
        using (var firstScope = _factory.Services.CreateScope())
        {
            InitializeTenantContext(firstScope);
            var firstStateMachine = firstScope.ServiceProvider.GetRequiredService<IStateMachine>();
            await firstStateMachine.ProcessMessageAsync("psid-conversation-4", "Toi muon mua kem lua", _factory.PrimaryPageId);
        }

        using var secondScope = _factory.Services.CreateScope();
        InitializeTenantContext(secondScope);
        var secondStateMachine = secondScope.ServiceProvider.GetRequiredService<IStateMachine>();
        var context = await secondStateMachine.LoadOrCreateAsync("psid-conversation-4", _factory.PrimaryPageId);

        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        (context.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Should().Contain("KL");
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_UsesRememberedContactWithSoftConfirmation()
    {
        using var scope = _factory.Services.CreateScope();
        InitializeTenantContext(scope);
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var reply = await stateMachine.ProcessMessageAsync(
            "psid-primary-existing",
            "Toi muon mua kem chong nang",
            _factory.PrimaryPageId);

        reply.Should().Contain("lan truoc");
        reply.Should().Contain("len don luon");
        reply.Should().NotContain("so dien thoai va dia chi em len don luon nha.");

        var context = await stateMachine.LoadOrCreateAsync("psid-primary-existing", _factory.PrimaryPageId);
        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
        context.GetData<string>("customerPhone").Should().Be("0911111111");
        context.GetData<string>("shippingAddress").Should().Be("22 Ly Tu Trong, Quan 1");
        context.GetData<bool?>("contactNeedsConfirmation").Should().BeTrue();
    }

    [Fact]
    public async Task ProcessMessage_ReturningCustomer_CanUpdateAddressWithoutReenteringPhone()
    {
        using var scope = _factory.Services.CreateScope();
        InitializeTenantContext(scope);
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        await stateMachine.ProcessMessageAsync(
            "psid-primary-existing",
            "Toi muon mua kem lua",
            _factory.PrimaryPageId);

        var reply = await stateMachine.ProcessMessageAsync(
            "psid-primary-existing",
            "Dia chi moi cua chi la 45 Nguyen Trai Quan 5",
            _factory.PrimaryPageId);

        reply.Should().Contain("don nhap");

        var draft = dbContext.DraftOrders
            .OrderByDescending(x => x.CreatedAt)
            .First(x => x.FacebookPSID == "psid-primary-existing");
        draft.CustomerPhone.Should().Be("0911111111");
        draft.ShippingAddress.Should().Contain("45 Nguyen Trai Quan 5");

        var customer = dbContext.CustomerIdentities.Single(x => x.FacebookPSID == "psid-primary-existing");
        customer.ShippingAddress.Should().Contain("45 Nguyen Trai Quan 5");
    }

    private ITenantContext InitializeTenantContext(IServiceScope scope)
    {
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Initialize(_factory.PrimaryTenantId, _factory.PrimaryPageId, _factory.PrimaryManagerEmail);
        return tenantContext;
    }
}
