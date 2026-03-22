using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class OrderConfirmationStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<OrderConfirmationStateHandler>> _loggerMock;
    private readonly OrderConfirmationStateHandler _handler;

    public OrderConfirmationStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<OrderConfirmationStateHandler>>();
        _handler = new OrderConfirmationStateHandler(
            _geminiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnOrderConfirmation()
    {
        Assert.Equal(ConversationState.OrderConfirmation, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithConfirmCommand_ShouldPlaceOrder()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderConfirmation };

        var response = await _handler.HandleAsync(ctx, "confirm");

        Assert.NotNull(ctx.GetData<string>("orderId"));
        Assert.Contains("Order Placed Successfully", response);
        Assert.Equal(ConversationState.OrderPlaced, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithBackCommand_ShouldReturnToPaymentMethod()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderConfirmation };

        var response = await _handler.HandleAsync(ctx, "back");

        Assert.Equal(ConversationState.PaymentMethod, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidInput_ShouldAskForConfirmation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderConfirmation };

        var response = await _handler.HandleAsync(ctx, "random text");

        Assert.Contains("confirm", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.OrderConfirmation, ctx.CurrentState);
    }
}
