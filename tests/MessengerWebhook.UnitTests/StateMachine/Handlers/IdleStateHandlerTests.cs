using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class IdleStateHandlerTests
{
    private readonly Mock<IProductMappingService> _productMappingService = new();
    private readonly Mock<IGiftSelectionService> _giftSelectionService = new();
    private readonly Mock<ICaseEscalationService> _caseEscalationService = new();
    private readonly Mock<ICustomerIntelligenceService> _customerIntelligenceService = new();
    private readonly Mock<IGeminiService> _geminiService = new();
    private readonly IdleStateHandler _handler;

    public IdleStateHandlerTests()
    {
        _customerIntelligenceService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        _customerIntelligenceService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });

        // Stub coordinator — not needed for these tests
        // Stub coordinator — not needed for these tests
        var mockDraftOrderService = Mock.Of<IDraftOrderService>();
        var draftOrderCoordinator = new DraftOrderCoordinator(
            mockDraftOrderService,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<DraftOrderCoordinator>>());

        _handler = new IdleStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            _productMappingService.Object,
            _giftSelectionService.Object,
            new FreeshipCalculator(),
            _caseEscalationService.Object,
            draftOrderCoordinator,
            _customerIntelligenceService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<IdleStateHandler>>());
    }

    [Fact]
    public void HandledState_ShouldReturnIdle()
    {
        Assert.Equal(ConversationState.Idle, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithProductIntent_ShouldMoveToCollectingInfo()
    {
        var ctx = new StateContext { FacebookPSID = "psid-1", CurrentState = ConversationState.Idle };
        _productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chống Nắng", BasePrice = 350000 });
        _giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(new Gift { Code = "GIFT", Name = "Quà mini" });

        // Mock AI intent detection - customer saying "I want to buy" is ReadyToBuy intent
        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                default))
            .ReturnsAsync(new MessengerWebhook.Services.AI.Models.IntentDetectionResult
            {
                Intent = MessengerWebhook.Services.AI.Models.CustomerIntent.ReadyToBuy,
                Confidence = 0.9,
                Reason = "Customer wants to buy product"
            });

        var response = await _handler.HandleAsync(ctx, "Tôi muốn mua kem chống nắng");

        Assert.Equal(ConversationState.CollectingInfo, ctx.CurrentState);
        Assert.Contains("Kem Chống Nắng", response);
        Assert.Contains("so dien thoai", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WithEscalationKeyword_ShouldMoveToHumanHandoff()
    {
        var ctx = new StateContext { FacebookPSID = "psid-2", CurrentState = ConversationState.Idle };
        _caseEscalationService
            .Setup(x => x.EscalateAsync(It.IsAny<string>(), It.IsAny<SupportCaseReason>(), It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(new HumanSupportCase { Id = Guid.NewGuid() });

        var response = await _handler.HandleAsync(ctx, "Tôi muốn refund ngay");

        Assert.Equal(ConversationState.HumanHandoff, ctx.CurrentState);
        Assert.Contains("chuyen", response, StringComparison.OrdinalIgnoreCase);
    }
}
