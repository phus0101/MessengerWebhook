using MessengerWebhook.Models;
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

        _handler = new CompleteStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            Mock.Of<IDraftOrderService>(),
            _customerService.Object,
            Options.Create(new SalesBotOptions()),
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
    public void HandledState_ShouldReturnComplete()
    {
        Assert.Equal(ConversationState.Complete, _handler.HandledState);
    }
}
