using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class VariantSelectionStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<ILogger<VariantSelectionStateHandler>> _loggerMock;
    private readonly VariantSelectionStateHandler _handler;

    public VariantSelectionStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _loggerMock = new Mock<ILogger<VariantSelectionStateHandler>>();
        _handler = new VariantSelectionStateHandler(
            _geminiServiceMock.Object,
            _stateMachineMock.Object,
            _productRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnVariantSelection()
    {
        Assert.Equal(ConversationState.VariantSelection, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithValidSelection_ShouldAddToCart()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.VariantSelection };
        ctx.SetData("selectedProductId", "prod1");
        var product = new Product
        {
            Id = "prod1",
            Name = "Test Product",
            Variants = new List<ProductVariant>
            {
                new() { Id = "var1", VolumeML = 50, Texture = "cream", Price = 29.99m, StockQuantity = 10 }
            }
        };

        _productRepositoryMock.Setup(x => x.GetByIdAsync("prod1")).ReturnsAsync(product);
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.AddToCart)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "1");

        Assert.Contains("var1", ctx.GetData<string>("selectedVariantId"));
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.AddToCart), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithBackCommand_ShouldReturnToProductDetail()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.VariantSelection };
        ctx.SetData("selectedProductId", "prod1");
        var product = new Product { Id = "prod1", Name = "Test Product" };

        _productRepositoryMock.Setup(x => x.GetByIdAsync("prod1")).ReturnsAsync(product);
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.ProductDetail)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "back");

        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.ProductDetail), Times.Once);
    }
}
