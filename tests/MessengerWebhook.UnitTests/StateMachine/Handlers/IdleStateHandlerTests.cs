using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
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
    private readonly Mock<IDraftOrderService> _draftOrderService = new();
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

        _handler = new IdleStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            _productMappingService.Object,
            _giftSelectionService.Object,
            new FreeshipCalculator(),
            _caseEscalationService.Object,
            _draftOrderService.Object,
            _customerIntelligenceService.Object,
            Options.Create(new SalesBotOptions()),
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
