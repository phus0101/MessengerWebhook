using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class OrderPlacedStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<OrderPlacedStateHandler>> _loggerMock;
    private readonly OrderPlacedStateHandler _handler;

    public OrderPlacedStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<OrderPlacedStateHandler>>();
        _handler = new OrderPlacedStateHandler(
            _geminiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnOrderPlaced()
    {
        Assert.Equal(ConversationState.OrderPlaced, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithTrackCommand_ShouldTransitionToOrderTracking()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderPlaced };
        ctx.SetData("orderId", "ORD-123");

        var response = await _handler.HandleAsync(ctx, "track order");

        Assert.Contains("Order Tracking", response);
        Assert.Equal(ConversationState.OrderTracking, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithMenuCommand_ShouldReturnToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderPlaced };

        var response = await _handler.HandleAsync(ctx, "menu");

        Assert.Contains("Main Menu", response);
        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithDefaultInput_ShouldShowDefaultMessage()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderPlaced };

        var response = await _handler.HandleAsync(ctx, "hello");

        Assert.Contains("order has been placed", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.OrderPlaced, ctx.CurrentState);
    }
}
