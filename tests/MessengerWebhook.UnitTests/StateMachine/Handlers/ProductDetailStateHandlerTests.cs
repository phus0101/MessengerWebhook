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

public class ProductDetailStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<ILogger<ProductDetailStateHandler>> _loggerMock;
    private readonly ProductDetailStateHandler _handler;

    public ProductDetailStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _loggerMock = new Mock<ILogger<ProductDetailStateHandler>>();
        _handler = new ProductDetailStateHandler(
            _geminiServiceMock.Object,
            _stateMachineMock.Object,
            _productRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnProductDetail()
    {
        Assert.Equal(ConversationState.ProductDetail, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithoutSelectedProduct_ShouldTransitionToBrowsing()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ProductDetail };
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "show details");

        Assert.Contains("select a product", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithVariantSelection_ShouldTransitionToVariantSelection()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ProductDetail };
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
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("select_variant");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.VariantSelection)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "select variant");

        Assert.Contains("50ml", response);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.VariantSelection), Times.Once);
    }
}
