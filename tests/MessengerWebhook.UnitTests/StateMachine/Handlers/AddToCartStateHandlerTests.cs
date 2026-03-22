using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class AddToCartStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<AddToCartStateHandler>> _loggerMock;
    private readonly AddToCartStateHandler _handler;

    public AddToCartStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<AddToCartStateHandler>>();
        _handler = new AddToCartStateHandler(_geminiServiceMock.Object, _stateMachineMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnAddToCart()
    {
        Assert.Equal(ConversationState.AddToCart, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithVariant_ShouldAddToCartAndTransition()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.AddToCart };
        ctx.SetData("selectedVariantId", "var1");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.CartReview)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "add");

        var cartItems = ctx.GetData<List<string>>("cartItems");
        Assert.NotNull(cartItems);
        Assert.Contains("var1", cartItems);
        Assert.Contains("Added to cart", response);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.CartReview), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutVariant_ShouldTransitionToBrowsing()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.AddToCart };
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "add");

        Assert.Contains("select a product", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }
}
