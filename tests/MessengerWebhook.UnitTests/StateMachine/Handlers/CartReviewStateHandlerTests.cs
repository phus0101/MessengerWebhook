using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class CartReviewStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<ILogger<CartReviewStateHandler>> _loggerMock;
    private readonly CartReviewStateHandler _handler;

    public CartReviewStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _loggerMock = new Mock<ILogger<CartReviewStateHandler>>();
        _handler = new CartReviewStateHandler(
            _geminiServiceMock.Object,
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

        var response = await _handler.HandleAsync(ctx, "view cart");

        Assert.Contains("empty", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
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

        var response = await _handler.HandleAsync(ctx, "checkout");

        Assert.Contains("checkout", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.ShippingAddress, ctx.CurrentState);
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

        var response = await _handler.HandleAsync(ctx, "continue shopping");

        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
    }
}
