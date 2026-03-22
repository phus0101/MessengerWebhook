using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class ShippingAddressStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<ShippingAddressStateHandler>> _loggerMock;
    private readonly ShippingAddressStateHandler _handler;

    public ShippingAddressStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<ShippingAddressStateHandler>>();
        _handler = new ShippingAddressStateHandler(
            _geminiServiceMock.Object,
            _stateMachineMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnShippingAddress()
    {
        Assert.Equal(ConversationState.ShippingAddress, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithValidAddress_ShouldTransitionToPayment()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ShippingAddress };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("valid");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.PaymentMethod)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "123 Main St, City, 12345");

        Assert.Equal("123 Main St, City, 12345", ctx.GetData<string>("shippingAddress"));
        Assert.Contains("payment", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.PaymentMethod), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidAddress_ShouldAskAgain()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ShippingAddress };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("invalid");
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "xyz");

        Assert.Contains("complete address", response, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ctx.GetData<string>("shippingAddress"));
    }

    [Fact]
    public async Task HandleAsync_WithBackCommand_ShouldReturnToCart()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ShippingAddress };
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.CartReview)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "back");

        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.CartReview), Times.Once);
    }
}
