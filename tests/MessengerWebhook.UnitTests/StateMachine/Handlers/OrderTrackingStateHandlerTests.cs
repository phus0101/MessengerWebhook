using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class OrderTrackingStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<OrderTrackingStateHandler>> _loggerMock;
    private readonly OrderTrackingStateHandler _handler;

    public OrderTrackingStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<OrderTrackingStateHandler>>();
        _handler = new OrderTrackingStateHandler(
            _geminiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnOrderTracking()
    {
        Assert.Equal(ConversationState.OrderTracking, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithMenuCommand_ShouldReturnToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderTracking };

        var response = await _handler.HandleAsync(ctx, "menu");

        Assert.Contains("Main Menu", response);
        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithDefaultInput_ShouldShowTrackingDetails()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderTracking };
        ctx.SetData("orderId", "ORD-123");

        var response = await _handler.HandleAsync(ctx, "status");

        Assert.Contains("Order Tracking", response);
        Assert.Contains("ORD-123", response);
        Assert.Equal(ConversationState.OrderTracking, ctx.CurrentState);
    }
}
