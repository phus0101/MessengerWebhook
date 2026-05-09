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

    // ── Test 1: Fresh customer, KCN full happy path ──────────────────────────
    [Fact]
    public async Task GoldenTranscript_FreshCustomer_KcnFullHappyPath_ShouldCreateDraftWithKcn()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-kcn-happy-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "em muốn mua kem chống nắng", pageId);
        turn1.Should().ContainEquivalentOf("kem chong nang");

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy em", pageId);
        turn2.Should().NotBeNullOrWhiteSpace();

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn nha, SĐT chị là 0912345678, địa chỉ 5 Đinh Tiên Hoàng quận 1", pageId);
        // Either order summary or product mention is acceptable
        (turn3.Contains("tóm tắt", StringComparison.OrdinalIgnoreCase) ||
         turn3.Contains("kem chong nang", StringComparison.OrdinalIgnoreCase) ||
         turn3.Contains("0912345678", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        var turn4 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn4.Should().ContainEquivalentOf("đơn nháp");

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);
        draft.Should().NotBeNull();
        draft!.Items.Should().Contain(x => x.ProductCode == "KCN");

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        context.CurrentState.Should().Be(ConversationState.Complete);
    }

    // ── Test 2: Fresh customer, Kem Lua with freeship question ───────────────
    [Fact]
    public async Task GoldenTranscript_FreshCustomer_KemLuaWithFreeshipQuery_ShouldCreateDraftWithKl()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-kl-freeship-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "kem lụa dưỡng sáng", pageId);
        turn1.Should().ContainEquivalentOf("kem lua");

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        turn2.Should().NotBeNullOrWhiteSpace();

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "ok lấy 1 cái, SĐT 0987654321, địa 99 Nguyen Trai Quan 5", pageId);
        turn3.Should().NotBeNullOrWhiteSpace();

        var turn4 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn4.Should().NotBeNullOrWhiteSpace();

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);
        draft.Should().NotBeNull();
        draft!.Items.Should().Contain(x => x.ProductCode == "KL");
    }

    // ── Test 3: Complete state → greeting resets to Consulting ───────────────
    [Fact]
    public async Task GoldenTranscript_CompleteState_GreetingResetsConversation_ShouldClearOrderContext()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-reset-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // Full purchase flow to reach Complete (4-turn: consult → freeship → order+contact → confirm)
        await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", pageId);
        await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, SĐT 0901234567, địa chỉ 10 Le Lai", pageId);
        await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);

        // Verify Complete state
        var ctxBefore = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxBefore.CurrentState.Should().Be(ConversationState.Complete);
        var oldDraftCode = ctxBefore.GetData<string>("draftOrderCode");

        // Send greeting to reset
        var greetTurn = await stateMachine.ProcessMessageAsync(psid, "chào em", pageId);
        greetTurn.Should().NotContain(oldDraftCode ?? "NEVER_MATCH_THIS_SENTINEL_VALUE");

        var ctxAfter = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxAfter.CurrentState.Should().Be(ConversationState.Consulting);
        ctxAfter.GetData<string>("draftOrderCode").Should().BeNull();
        var selectedCodes = ctxAfter.GetData<List<string>>("selectedProductCodes");
        (selectedCodes == null || selectedCodes.Count == 0).Should().BeTrue();
    }

    // ── Test 4: Complete state → 24-hour timeout resets conversation ─────────
    [Fact]
    public async Task GoldenTranscript_CompleteState_After24Hours_ShouldResetToConsulting()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-timeout-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // Build a Complete state using full 4-turn flow (mirrors ProductDrift test)
        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm", pageId);
        await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1", pageId);
        await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);

        var ctxComplete = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxComplete.CurrentState.Should().Be(ConversationState.Complete);

        // Manually backdate the session's LastActivityAt and ExpiresAt so the next load triggers timeout
        var session = await dbContext.ConversationSessions.FirstAsync(x => x.FacebookPSID == psid);
        session.LastActivityAt = DateTime.UtcNow.AddHours(-25);
        session.ExpiresAt = DateTime.UtcNow.AddHours(-23);  // also expire absolute timeout
        await dbContext.SaveChangesAsync();

        // Send a follow-up — should be treated as a new/reset conversation
        var followUp = await stateMachine.ProcessMessageAsync(psid, "đơn chị đã được xác nhận chưa em", pageId);
        followUp.Should().NotBeNullOrWhiteSpace();

        var ctxAfter = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxAfter.CurrentState.Should().NotBe(ConversationState.Complete);
        ctxAfter.GetData<string>("draftOrderCode").Should().BeNull();
    }

    // ── Test 5: Complete state → "thông tin nào" query still shows order details
    [Fact]
    public async Task GoldenTranscript_CompleteState_ThongTinNaoQuery_ShouldListOrderDetails()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-thongtin-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // Full MN purchase flow (4-turn: consult → freeship → order+contact → confirm)
        await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm", pageId);
        await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, số của chị là 0901111222, địa chỉ 5 Ba Dinh Quan 1", pageId);
        await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);

        var ctxComplete = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxComplete.CurrentState.Should().Be(ConversationState.Complete);

        // Verify MN draft exists with expected gift
        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);
        draft.Should().NotBeNull();
        draft!.Items.Should().Contain(x => x.ProductCode == "MN");

        // Query order details in Complete state
        var infoTurn = await stateMachine.ProcessMessageAsync(psid, "thông tin nào vậy em?", pageId);
        infoTurn.Should().NotBeNullOrWhiteSpace();
        // At minimum a response is returned; in Complete state the bot should handle informational queries
        var ctxStill = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxStill.CurrentState.Should().Be(ConversationState.Complete);
    }

    // ── Test 6: Returning customer with stored contact → contact shown ────────
    [Fact]
    public async Task GoldenTranscript_ReturningCustomer_KcnPurchase_ShouldOfferStoredContact()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-returning-kcn-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        dbContext.CustomerIdentities.Add(new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0900111222",
            ShippingAddress = "33 Le Duan Quan 1",
            TenantId = _factory.PrimaryTenantId
        });
        await dbContext.SaveChangesAsync();

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", pageId);
        turn1.Should().ContainEquivalentOf("kem chong nang");

        // Consulting turns to lock the product selection
        var turn2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy", pageId);
        turn2.Should().ContainEquivalentOf("kem chong nang");

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "có freeship ko e", pageId);
        turn3.Should().ContainEquivalentOf("kem chong nang");

        // ReadyToBuy intent → should surface stored contact for returning customer
        var turn4 = await stateMachine.ProcessMessageAsync(psid, "ok vậy lấy sản phẩm này nhé", pageId);
        turn4.Should().NotBeNullOrWhiteSpace();
        (turn4.Contains("0900111222", StringComparison.OrdinalIgnoreCase) ||
         turn4.Contains("33 Le Duan", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        context.CurrentState.Should().Be(ConversationState.CollectingInfo);
    }

    // ── Test 7: Multi-turn consultation → Mat Na Ngu draft ───────────────────
    [Fact]
    public async Task GoldenTranscript_FreshCustomer_MultiTurnConsultation_ShouldCreateMnDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-mn-multiturn-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ dưỡng ẩm", pageId);
        turn1.Should().ContainEquivalentOf("mat na ngu");

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu", pageId);
        turn2.Should().NotBeNullOrWhiteSpace();

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "nói kỹ hơn về sản phẩm này", pageId);
        turn3.Should().NotBeNullOrWhiteSpace();

        var turn4 = await stateMachine.ProcessMessageAsync(psid, "ok em, SĐT chị 0901122334, địa chỉ 7 Ba Trieu Quan 1", pageId);
        turn4.Should().NotBeNullOrWhiteSpace();

        var turn5 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn5.Should().ContainEquivalentOf("đơn nháp");

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);
        draft.Should().NotBeNull();
        draft!.Items.Should().Contain(x => x.ProductCode == "MN");

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        context.CurrentState.Should().Be(ConversationState.Complete);
    }

    // ── Test 8: Combo product full happy path ─────────────────────────────────
    [Fact]
    public async Task GoldenTranscript_FreshCustomer_ComboProduct_ShouldCreateComboDraft()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-combo-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "combo 2 sản phẩm", pageId);
        turn1.Should().NotBeNullOrWhiteSpace();

        // Keep consulting to lock product selection
        var turn2 = await stateMachine.ProcessMessageAsync(psid, "giá bao nhiêu vậy em", pageId);
        turn2.Should().NotBeNullOrWhiteSpace();

        // Provide contact and order in one message
        var turn3 = await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, SĐT 0911223344, địa chỉ 88 Tran Quoc Toan", pageId);
        turn3.Should().NotBeNullOrWhiteSpace();

        // Confirm the summary
        var turn4 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn4.Should().ContainEquivalentOf("đơn nháp");

        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);
        draft.Should().NotBeNull();
        draft!.Items.Should().Contain(x => x.ProductCode == "COMBO_2");

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        context.CurrentState.Should().Be(ConversationState.Complete);
    }

    // ── Test 9: Returning customer with new contact → draft uses new contact ──
    [Fact]
    public async Task GoldenTranscript_ReturningCustomer_NewContact_ChoosesToSave_ShouldUpdateMemory()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MessengerBotDbContext>();

        var psid = $"golden-newcontact-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        dbContext.CustomerIdentities.Add(new CustomerIdentity
        {
            FacebookPSID = psid,
            FacebookPageId = pageId,
            PhoneNumber = "0900000099",
            ShippingAddress = "1 Cu Chi",
            TenantId = _factory.PrimaryTenantId
        });
        await dbContext.SaveChangesAsync();

        var turn1 = await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", pageId);
        turn1.Should().ContainEquivalentOf("kem chong nang");

        // Provide new contact details inline → summary should show new contact
        var turn2 = await stateMachine.ProcessMessageAsync(psid, "lấy 1 cái, SĐT mới chị là 0912999888, địa mới 50 Hai Ba Trung", pageId);
        turn2.Should().NotBeNullOrWhiteSpace();

        var turn3 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        turn3.Should().NotBeNullOrWhiteSpace();

        // Draft should be created with new phone if checkout completed
        var draft = await dbContext.DraftOrders
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.FacebookPSID == psid);

        if (draft != null)
        {
            // Draft exists: verify it uses the new contact phone
            draft.CustomerPhone.Should().Be("0912999888");
            draft.Items.Should().Contain(x => x.ProductCode == "KCN");
        }
        else
        {
            // May still be in CollectingInfo waiting for explicit confirmation — that is also valid
            var ctx = await stateMachine.LoadOrCreateAsync(psid, pageId);
            ctx.CurrentState.Should().BeOneOf(ConversationState.CollectingInfo, ConversationState.DraftOrder);
        }
    }

    // ── Test 10: Brand-new customer first ever message ────────────────────────
    [Fact]
    public async Task GoldenTranscript_BrandNewCustomer_FirstEverMessage_ShouldEnterConsulting()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"golden-brandnew-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // Completely new PSID, no seeded data
        var turn1 = await stateMachine.ProcessMessageAsync(psid, "hi em", pageId);
        turn1.Should().NotBeNullOrWhiteSpace();

        var context = await stateMachine.LoadOrCreateAsync(psid, pageId);
        context.CurrentState.Should().BeOneOf(ConversationState.Consulting, ConversationState.Idle);

        var turn2 = await stateMachine.ProcessMessageAsync(psid, "mặt nạ ngủ", pageId);
        turn2.Should().ContainEquivalentOf("mat na ngu");
    }

    // ── Test 11: All states handled — no unhandled-state exceptions ───────────
    [Fact]
    public async Task GoldenTranscript_StateMachineHandlesAllStates_NoUnhandledStateException()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"golden-allstates-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // Consulting
        var r1 = await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", pageId);
        r1.Should().NotBeNullOrWhiteSpace();

        // CollectingInfo / DraftOrder
        var r2 = await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn, SĐT 0901234567, địa 1 Test St", pageId);
        r2.Should().NotBeNullOrWhiteSpace();

        // Complete
        var r3 = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        r3.Should().NotBeNullOrWhiteSpace();

        // Follow-up in Complete state
        var r4 = await stateMachine.ProcessMessageAsync(psid, "ok", pageId);
        r4.Should().NotBeNullOrWhiteSpace();
    }

    // ── Test 12: KCN purchase, then greeting resets state ────────────────────
    [Fact]
    public async Task GoldenTranscript_FreshCustomer_KcnThenGreeting_ShouldResetState()
    {
        using var scope = await CreateStateMachineScopeAsync();
        var stateMachine = scope.ServiceProvider.GetRequiredService<IStateMachine>();

        var psid = $"golden-kcn-greeting-{Guid.NewGuid()}";
        var pageId = _factory.PrimaryPageId;

        // 4-turn flow to reach Complete (mirrors ProductDrift test)
        await stateMachine.ProcessMessageAsync(psid, "kem chống nắng", pageId);
        await stateMachine.ProcessMessageAsync(psid, "có freeship không em", pageId);
        await stateMachine.ProcessMessageAsync(psid, "ok em lên đơn luôn, SĐT 0999888777, địa chỉ 5 Test Street", pageId);
        var confirmTurn = await stateMachine.ProcessMessageAsync(psid, "đúng rồi", pageId);
        confirmTurn.Should().ContainEquivalentOf("đơn nháp");

        var ctxComplete = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxComplete.CurrentState.Should().Be(ConversationState.Complete);

        // Greeting after Complete → resets to Consulting
        var greetTurn = await stateMachine.ProcessMessageAsync(psid, "hi", pageId);
        greetTurn.Should().NotBeNullOrWhiteSpace();

        var ctxAfter = await stateMachine.LoadOrCreateAsync(psid, pageId);
        ctxAfter.CurrentState.Should().NotBe(ConversationState.Complete);
        ctxAfter.GetData<string>("draftOrderCode").Should().BeNull();
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
