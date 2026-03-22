using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class CartReviewStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<ILogger<CartReviewStateHandler>> _loggerMock;
    private readonly CartReviewStateHandler _handler;

    public CartReviewStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _loggerMock = new Mock<ILogger<CartReviewStateHandler>>();
        _handler = new CartReviewStateHandler(
            _geminiServiceMock.Object,
            _stateMachineMock.Object,
            _productRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnCartReview()
    {
        Assert.Equal(ConversationState.CartReview, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyCart_ShouldTransitionToBrowsing()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CartReview };
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "view cart");

        Assert.Contains("empty", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithCheckoutIntent_ShouldTransitionToShipping()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CartReview };
        ctx.SetData("cartItems", new List<string> { "item1", "item2" });

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("checkout");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.ShippingAddress)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "checkout");

        Assert.Contains("checkout", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.ShippingAddress), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithContinueShopping_ShouldTransitionToBrowsing()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CartReview };
        ctx.SetData("cartItems", new List<string> { "item1" });

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("continue_shopping");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "continue shopping");

        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }
}
