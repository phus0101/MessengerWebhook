using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class PaymentMethodStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<PaymentMethodStateHandler>> _loggerMock;
    private readonly PaymentMethodStateHandler _handler;

    public PaymentMethodStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<PaymentMethodStateHandler>>();
        _handler = new PaymentMethodStateHandler(
            _geminiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnPaymentMethod()
    {
        Assert.Equal(ConversationState.PaymentMethod, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithValidPaymentMethod_ShouldTransitionToOrderConfirmation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.PaymentMethod };
        ctx.SetData("shippingAddress", "123 Main St");
        ctx.SetData("cartItems", new List<string> { "item1", "item2" });

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("credit card");

        var response = await _handler.HandleAsync(ctx, "1");

        Assert.Equal("credit card", ctx.GetData<string>("paymentMethod"));
        Assert.Contains("Order Summary", response);
        Assert.Equal(ConversationState.OrderConfirmation, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithBackCommand_ShouldReturnToShippingAddress()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.PaymentMethod };

        var response = await _handler.HandleAsync(ctx, "back");

        Assert.Equal(ConversationState.ShippingAddress, ctx.CurrentState);
    }
}
