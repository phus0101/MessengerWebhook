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
}
