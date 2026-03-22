using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class BrowsingProductsStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IVectorSearchRepository> _vectorSearchMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<BrowsingProductsStateHandler>> _loggerMock;
    private readonly BrowsingProductsStateHandler _handler;

    public BrowsingProductsStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _vectorSearchMock = new Mock<IVectorSearchRepository>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<BrowsingProductsStateHandler>>();
        _handler = new BrowsingProductsStateHandler(
            _geminiServiceMock.Object,
            _vectorSearchMock.Object,
            _embeddingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnBrowsingProducts()
    {
        Assert.Equal(ConversationState.BrowsingProducts, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithCartCommand_ShouldTransitionToCartReview()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.BrowsingProducts };
        ctx.SetData("cartItems", new List<string> { "item1" });

        var response = await _handler.HandleAsync(ctx, "show cart");

        Assert.Contains("cart", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.CartReview, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithSearchQuery_ShouldReturnProducts()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.BrowsingProducts };
        var products = new List<Product>
        {
            new() { Id = "1", Name = "Moisturizer", Brand = "Brand A", BasePrice = 29.99m, Description = "Great moisturizer for all skin types" }
        };

        _embeddingServiceMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), default)).ReturnsAsync(new float[768]);
        _vectorSearchMock.Setup(x => x.SearchSimilarProductsAsync(It.IsAny<float[]>(), 5, 0.7, default))
            .ReturnsAsync(products);

        var response = await _handler.HandleAsync(ctx, "moisturizer");

        Assert.Contains("Moisturizer", response);
        Assert.Contains("Brand A", response);
        _embeddingServiceMock.Verify(x => x.GenerateAsync(It.IsAny<string>(), default), Times.Once);
        _vectorSearchMock.Verify(x => x.SearchSimilarProductsAsync(It.IsAny<float[]>(), 5, 0.7, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNoResults_ShouldReturnHelpfulMessage()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.BrowsingProducts };
        _embeddingServiceMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), default)).ReturnsAsync(new float[768]);
        _vectorSearchMock.Setup(x => x.SearchSimilarProductsAsync(It.IsAny<float[]>(), 5, 0.7, default))
            .ReturnsAsync(new List<Product>());

        var response = await _handler.HandleAsync(ctx, "xyz123");

        Assert.Contains("couldn't find", response, StringComparison.OrdinalIgnoreCase);
    }
}
