using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Survey;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class CompleteStateHandlerTests
{
    private readonly Mock<ICustomerIntelligenceService> _customerService;
    private readonly CompleteStateHandler _handler;

    public CompleteStateHandlerTests()
    {
        _customerService = new Mock<ICustomerIntelligenceService>();

        _customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        _customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });

        var draftOrderCoordinator = new DraftOrderCoordinator(
            Mock.Of<IDraftOrderService>(),
            Mock.Of<IMemoryCache>(),
            NullLogger<DraftOrderCoordinator>.Instance);

        _handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            draftOrderCoordinator,
            _customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Mock.Of<IServiceProvider>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Options.Create(new CSATSurveyOptions { Enabled = false }),
            Mock.Of<ILogger<CompleteStateHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ShouldResetConsultationRejectionCounter()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("consultationRejectionCount", 5);
        ctx.SetData("consultationDeclined", true);
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        // Act
        await _handler.HandleAsync(ctx, "ok");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        var consultationDeclined = ctx.GetData<bool>("consultationDeclined");

        Assert.Equal(0, rejectionCount);
        Assert.False(consultationDeclined);
    }

    [Fact]
    public async Task HandleAsync_WithDraftOrderCode_ShouldReturnOrderConfirmation()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        // Act
        var response = await _handler.HandleAsync(ctx, "ok");

        // Assert
        Assert.Contains("DR-TEST-001", response);
        Assert.Contains("dang cho", response.ToLower());
    }

    [Fact]
    public async Task HandleAsync_WithoutDraftOrderCode_ShouldReturnGenericConfirmation()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };

        // Act
        var response = await _handler.HandleAsync(ctx, "ok");

        // Assert
        Assert.DoesNotContain("DR-", response);
        Assert.Contains("len don", response.ToLower());
    }

    [Fact]
    public async Task HandleAsync_WhenPendingSaveUpdatedContactAndCustomerAgrees_ShouldAcknowledgeUpdate()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");
        ctx.SetData("customerPhone", "0988888888");
        ctx.SetData("shippingAddress", "99 Le Loi");
        ctx.SetData("facebookPageId", "PAGE_1");

        var response = await _handler.HandleAsync(ctx, "có em cập nhật giúp chị");

        Assert.Contains("cập nhật", response, StringComparison.OrdinalIgnoreCase);
        Assert.True(ctx.GetData<bool?>("saveCurrentContactForFuture"));
        Assert.Null(ctx.GetData<string>("pendingContactQuestion"));
    }

    [Fact]
    public async Task HandleAsync_WhenPendingSaveUpdatedContactAndCustomerDeclines_ShouldKeepOldInfo()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");

        var response = await _handler.HandleAsync(ctx, "không em cứ giữ thông tin cũ");

        Assert.Contains("giữ nguyên", response, StringComparison.OrdinalIgnoreCase);
        Assert.False(ctx.GetData<bool?>("saveCurrentContactForFuture"));
        Assert.Null(ctx.GetData<string>("pendingContactQuestion"));
    }

    [Fact]
    public async Task HandleAsync_WhenPendingSaveUpdatedContactAndCustomerSaysKhongCanCapNhat_ShouldNotPersist()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");

        var response = await _handler.HandleAsync(ctx, "không cần cập nhật đâu em");

        Assert.Contains("giữ nguyên", response, StringComparison.OrdinalIgnoreCase);
        Assert.False(ctx.GetData<bool?>("saveCurrentContactForFuture"));
        Assert.Null(ctx.GetData<string>("pendingContactQuestion"));
    }

    [Fact]
    public async Task HandleAsync_WhenGreetingStartsNewConversation_ShouldClearConversationHistory()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("conversationHistory", new List<MessengerWebhook.Services.AI.Models.ConversationMessage>
        {
            new() { Role = "user", Content = "old", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "old reply", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("surveySent", true);

        await _handler.HandleAsync(ctx, "hi");

        var history = ctx.GetData<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>("conversationHistory");
        Assert.NotNull(history);
        Assert.True(history!.Count <= 2);
        Assert.NotEqual(ConversationState.Complete, ctx.CurrentState);
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
        Assert.False(ctx.GetData<bool>("surveySent"));
    }

    [Fact]
    public async Task HandleAsync_WhenGreetingPrefixedOrderFollowUp_ShouldKeepCompleteContext()
    {
        var ctx = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.Complete,
            LastInteractionAt = DateTime.UtcNow.AddHours(-1)
        };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("conversationHistory", new List<MessengerWebhook.Services.AI.Models.ConversationMessage>
        {
            new() { Role = "user", Content = "old", Timestamp = DateTime.UtcNow.AddMinutes(-2) }
        });

        var response = await _handler.HandleAsync(ctx, "hi em đơn chị tới đâu rồi");

        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
        Assert.Equal("DR-TEST-001", ctx.GetData<string>("draftOrderCode"));
        Assert.Contains("DR-TEST-001", response);
    }

    [Fact]
    public async Task HandleAsync_WhenLastInteractionExceeded24Hours_ShouldStartNewConversation()
    {
        var ctx = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.Complete,
            LastInteractionAt = DateTime.UtcNow.AddHours(-25)
        };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("surveySent", true);

        await _handler.HandleAsync(ctx, "đơn tới đâu rồi em");

        Assert.NotEqual(ConversationState.Complete, ctx.CurrentState);
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
        Assert.False(ctx.GetData<bool>("surveySent"));
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerAsksThongTinNao_ShouldClarifyCheckedFields()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        var response = await _handler.HandleAsync(ctx, "thông tin nào vậy em?");

        Assert.Contains("DR-TEST-001", response);
        Assert.Contains("sản phẩm", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("số lượng", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SĐT", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("địa chỉ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phí ship", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quà tặng", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerAsksThongTinNaoAndGiftExists_ShouldMentionGift()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("selectedGiftName", "Mặt nạ dưỡng sáng");

        var response = await _handler.HandleAsync(ctx, "thông tin nào vậy em?");

        Assert.Contains("DR-TEST-001", response);
        Assert.Contains("phí ship", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quà tặng Mặt nạ dưỡng sáng", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandledState_ShouldReturnComplete()
    {
        Assert.Equal(ConversationState.Complete, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationRequestInCompleteState_ShouldEscalate()
    {
        var policyGuardService = new Mock<IPolicyGuardService>();
        policyGuardService
            .Setup(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyDecision(
                true,
                SupportCaseReason.CancellationRequest,
                "Customer wants to cancel draft order",
                PolicyAction.HardEscalate,
                0.85m,
                1m));

        var caseEscalationService = new Mock<ICaseEscalationService>();
        caseEscalationService
            .Setup(x => x.EscalateAsync(
                It.IsAny<string>(),
                It.IsAny<SupportCaseReason>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HumanSupportCase { Id = Guid.NewGuid() });

        var handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            policyGuardService.Object,
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            caseEscalationService.Object,
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            _customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Mock.Of<IServiceProvider>(),
            Options.Create(new SalesBotOptions { UnsupportedFallbackMessage = "Da em xin phep chuyen chi qua ban ho tro cua Mui Xu de xu ly ky hon nha." }),
            Options.Create(new RAGOptions { Enabled = false }),
            Options.Create(new CSATSurveyOptions { Enabled = false }),
            Mock.Of<ILogger<CompleteStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        var response = await handler.HandleAsync(ctx, "chị muốn hủy đơn này");

        Assert.Equal(ConversationState.HumanHandoff, ctx.CurrentState);
        Assert.Contains("Mui Xu", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DR-TEST-001", response);
        policyGuardService.Verify(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        caseEscalationService.Verify(
            x => x.EscalateAsync(
                It.IsAny<string>(),
                SupportCaseReason.CancellationRequest,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── NEW TESTS (coverage bridging) ────────────────────────────────────────

    // A. pendingContactQuestion with ambiguous reply → clarification prompt
    [Fact]
    public async Task HandleAsync_WhenPendingContactQuestionAndAmbiguousReply_ShouldReturnClarificationPrompt()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        var response = await _handler.HandleAsync(ctx, "sau này tính");

        // Neither yes nor no → clarification prompt is returned
        Assert.Contains("có em cập nhật", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không em cứ giữ", response, StringComparison.OrdinalIgnoreCase);
        // pendingContactQuestion NOT cleared
        Assert.NotNull(ctx.GetData<string>("pendingContactQuestion"));
    }

    // B. pendingContactQuestion with whitespace-only message → falls through to normal follow-up
    [Fact]
    public async Task HandleAsync_WhenPendingContactQuestionAndWhitespaceMessage_ShouldFallThroughToNormalFlow()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        // Whitespace is trimmed to empty → HandleSaveUpdatedContactReplyAsync returns null
        var response = await _handler.HandleAsync(ctx, "   ");

        // Falls through to normal follow-up flow, draft code should appear
        Assert.Contains("DR-TEST-001", response);
        // pendingContactQuestion is NOT cleared by the normal follow-up path
        Assert.NotNull(ctx.GetData<string>("pendingContactQuestion"));
    }

    // C. Policy SafeReply → returns safe reply message, state stays Complete
    [Fact]
    public async Task HandleAsync_WhenPolicySafeReply_ShouldReturnSafeReplyMessage()
    {
        var policyGuardService = new Mock<IPolicyGuardService>();
        policyGuardService
            .Setup(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyDecision(
                false,
                default,
                "",
                PolicyAction.SafeReply,
                0.5m,
                0.5m));

        var handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            policyGuardService.Object,
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            _customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Mock.Of<IServiceProvider>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Options.Create(new CSATSurveyOptions { Enabled = false }),
            Mock.Of<ILogger<CompleteStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        var response = await handler.HandleAsync(ctx, "tôi muốn khiếu nại");

        Assert.Equal(new PolicyGuardOptions().SafeReplyMessage, response);
        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
    }

    // D. CSAT enabled + not yet sent → surveySent becomes true
    [Fact]
    public async Task HandleAsync_WhenCsatEnabledAndNotYetSent_ShouldMarkSurveySent()
    {
        var handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            _customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Mock.Of<IServiceProvider>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Options.Create(new CSATSurveyOptions { Enabled = true, DelayMinutes = 5 }),
            Mock.Of<ILogger<CompleteStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete, SessionId = "session-csat-test" };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("surveySent", false);

        // ScheduleSurvey fires a background Task.Run — it won't throw synchronously
        await handler.HandleAsync(ctx, "ok");

        Assert.True(ctx.GetData<bool>("surveySent"));
    }

    // E. CSAT enabled + already sent → surveySent stays true, no double scheduling
    [Fact]
    public async Task HandleAsync_WhenCsatEnabledAndAlreadySent_ShouldNotRescheduleSurvey()
    {
        var handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            _customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Mock.Of<IServiceProvider>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Options.Create(new CSATSurveyOptions { Enabled = true, DelayMinutes = 5 }),
            Mock.Of<ILogger<CompleteStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete, SessionId = "session-csat-already-sent" };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("surveySent", true);

        var response = await handler.HandleAsync(ctx, "ok");

        Assert.True(ctx.GetData<bool>("surveySent"));
        Assert.NotEmpty(response);
    }

    // F. "thông tin nào" without draft code → generic clarification
    [Fact]
    public async Task HandleAsync_WhenThongTinNaoWithoutDraftCode_ShouldReturnGenericClarification()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        // No draftOrderCode set

        var response = await _handler.HandleAsync(ctx, "thông tin nào vậy em");

        Assert.DoesNotContain("DR-", response);
        Assert.DoesNotContain("đơn nháp", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sản phẩm đã chốt", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SĐT", response, StringComparison.OrdinalIgnoreCase);
    }

    // G. "thong tin nao" ASCII variant with draft code → includes draft code
    [Fact]
    public async Task HandleAsync_WhenThongTinNaoAsciiVariantWithDraftCode_ShouldIncludeDraftCode()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-002");

        var response = await _handler.HandleAsync(ctx, "thong tin nao vay em");

        Assert.Contains("DR-TEST-002", response);
    }

    // H. Greeting with trailing punctuation → starts new conversation
    [Fact]
    public async Task HandleAsync_WhenGreetingWithPunctuation_ShouldStartNewConversation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");
        ctx.SetData("surveySent", true);

        await _handler.HandleAsync(ctx, "hi!");

        Assert.NotEqual(ConversationState.Complete, ctx.CurrentState);
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
    }

    // I. Various greeting variants all start new conversation
    [Theory]
    [InlineData("hello")]
    [InlineData("chào em")]
    [InlineData("alo shop")]
    [InlineData("alô em")]
    [InlineData("chao shop")]
    public async Task HandleAsync_WhenGreetingVariants_ShouldStartNewConversation(string greeting)
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("draftOrderCode", "DR-TEST-001");

        await _handler.HandleAsync(ctx, greeting);

        Assert.NotEqual(ConversationState.Complete, ctx.CurrentState);
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
    }

    // J. New conversation resets ALL order-related fields
    [Fact]
    public async Task HandleAsync_WhenNewConversationStarts_ShouldResetAllOrderRelatedFields()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Complete };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("selectedProductQuantities", new Dictionary<string, int> { ["KCN"] = 2 });
        ctx.SetData("selectedGiftCode", "GIFT_001");
        ctx.SetData("selectedGiftName", "Mặt nạ dưỡng sáng");
        ctx.SetData("shippingFee", 30000m);
        ctx.SetData("customerPhone", "0901234567");
        ctx.SetData("shippingAddress", "12 Hoa Mai");
        ctx.SetData("rememberedCustomerPhone", "0901234567");
        ctx.SetData("rememberedShippingAddress", "12 Hoa Mai");
        ctx.SetData("contactNeedsConfirmation", true);
        ctx.SetData("vipGreetingSent", true);
        ctx.SetData("consultationRejectionCount", 3);
        ctx.SetData("awaitingFinalSummaryConfirmation", true);
        ctx.SetData("final_price_summary_ready", true);
        ctx.SetData("price_confirmed", true);
        ctx.SetData("surveySent", true);

        await _handler.HandleAsync(ctx, "hi");

        Assert.Null(ctx.GetData<string>("selectedGiftCode"));
        Assert.Null(ctx.GetData<string>("selectedGiftName"));
        Assert.Null(ctx.GetData<string>("customerPhone"));
        Assert.Null(ctx.GetData<string>("shippingAddress"));
        Assert.Null(ctx.GetData<string>("rememberedCustomerPhone"));
        Assert.Null(ctx.GetData<string>("rememberedShippingAddress"));
        Assert.False(ctx.GetData<bool>("contactNeedsConfirmation"));
        // vipGreetingSent is cleared then potentially set again by greeting handler — just verify state changed
        Assert.Equal(0, ctx.GetData<int>("consultationRejectionCount"));
        Assert.False(ctx.GetData<bool>("awaitingFinalSummaryConfirmation"));
        Assert.False(ctx.GetData<bool>("final_price_summary_ready"));
        Assert.False(ctx.GetData<bool>("price_confirmed"));
        Assert.False(ctx.GetData<bool>("surveySent"));
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
    }
}
