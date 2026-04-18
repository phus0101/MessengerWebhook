using FluentAssertions;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine;
using MessengerWebhook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MessengerWebhook.IntegrationTests.StateMachine;

public class TranscriptGoldenFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TranscriptGoldenFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GoldenTranscript_ReturningCustomer_GenericOkThenExplicitConfirm_ShouldReaskFullContactBeforeCreatingDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-returning-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        dbContext.CustomerIdentities.Add(new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0911222333",
            ShippingAddress = "12 Tran Hung Dao, Quan 1",
            TenantId = _factory.PrimaryTenantId
        });
        await dbContext.SaveChangesAsync();

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "cho em biết thêm về mặt nạ ngủ dưỡng ẩm", pageId);
        turn1.Should().ContainEquivalentOf("mat na ngu");
        turn1.Should().NotContainEquivalentOf("kem lua");

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy", pageId);
        turn2.Should().ContainEquivalentOf("mat na ngu");

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "có freeship ko e", pageId);
        turn3.Should().ContainEquivalentOf("mat na ngu");
        turn3.Should().NotContainEquivalentOf("kem lua");

        var turn4 = await stateMachine.ProcessMessageAsync(psid, "ok vậy lấy sản phẩm này nhé", pageId);
        turn4.Should().Contain("0911222333");
        turn4.Should().ContainEquivalentOf("12 Tran Hung Dao");
        turn4.Should().NotContainEquivalentOf("đơn nháp");
        turn4.Should().NotContainEquivalentOf("DR-");

        var turn5 = await stateMachine.ProcessMessageAsync(psid, "ok", pageId);
        turn5.Should().Contain("0911222333");
        turn5.Should().ContainEquivalentOf("12 Tran Hung Dao");
        turn5.Should().ContainEquivalentOf("xác nhận");
        turn5.Should().NotContainEquivalentOf("đơn nháp");

        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var pendingContext = await stateMachine.LoadOrCreateAsync(psid, pageId);
        pendingContext.CurrentState.Should().Be(ConversationState.CollectingInfo);
        pendingContext.GetData<bool?>("contactNeedsConfirmation").Should().BeTrue();
        pendingContext.GetData<string>("pendingContactQuestion").Should().Be("confirm_old_contact");
        (pendingContext.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Should().ContainSingle().Which.Should().Be("MN");

        var turn6 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn6.Should().ContainEquivalentOf("tóm tắt đơn");
        turn6.Should().ContainEquivalentOf("mat na ngu");
        turn6.Should().Contain("0911222333");
        turn6.Should().ContainEquivalentOf("12 Tran Hung Dao");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var turn7 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn7.Should().ContainEquivalentOf("đơn nháp");
        turn7.Should().ContainEquivalentOf("mat na ngu");
        turn7.Should().NotContainEquivalentOf("kem lụa");

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(x => x.FacebookPSID == psid);

        draft.CustomerPhone.Should().Be("0911222333");
        draft.ShippingAddress.Should().Be("12 Tran Hung Dao, Quan 1");
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");
        draft.Items.Should().NotContain(x => x.ProductCode == "KL");
    }

    [Fact]
    public async Task GoldenTranscript_ProductDriftAttempt_DuringPolicyAndCheckout_ShouldKeepLockedProductInDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-lock-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm", pageId);
        turn1.Should().ContainEquivalentOf("mat na ngu");

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ này có khuyến mãi gì không em, kem lụa chị chưa cần", pageId);
        turn2.Should().ContainEquivalentOf("mat na ngu");
        turn2.Should().NotContainEquivalentOf("kem lua");

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        turn3.Should().ContainEquivalentOf("mat na ngu");
        turn3.Should().NotContainEquivalentOf("kem lua");

        var turn4 = await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1", pageId);
        turn4.Should().ContainEquivalentOf("tóm tắt đơn");
        turn4.Should().ContainEquivalentOf("mat na ngu");
        turn4.Should().NotContainEquivalentOf("kem lua");
        dbContext.DraftOrders.Any(x => x.FacebookPSID == psid).Should().BeFalse();

        var turn5 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn5.Should().ContainEquivalentOf("đơn nháp");
        turn5.Should().ContainEquivalentOf("mat na ngu");
        turn5.Should().NotContainEquivalentOf("kem lua");

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        (context.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Should().ContainSingle().Which.Should().Be("MN");
        context.CurrentState.Should().Be(ConversationState.Complete);

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .SingleAsync(x => x.FacebookPSID == psid);
        draft.Items.Should().ContainSingle(x => x.ProductCode == "MN");
        draft.Items.Should().NotContain(x => x.ProductCode == "KL");
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
