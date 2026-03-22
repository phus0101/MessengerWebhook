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

public class SkinAnalysisStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<IVectorSearchRepository> _vectorSearchMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<SkinAnalysisStateHandler>> _loggerMock;
    private readonly SkinAnalysisStateHandler _handler;

    public SkinAnalysisStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _vectorSearchMock = new Mock<IVectorSearchRepository>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<SkinAnalysisStateHandler>>();
        _handler = new SkinAnalysisStateHandler(
            _geminiServiceMock.Object,
            _stateMachineMock.Object,
            _vectorSearchMock.Object,
            _embeddingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnSkinAnalysis()
    {
        Assert.Equal(ConversationState.SkinAnalysis, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithSkinType_ShouldRecommendProducts()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinAnalysis };
        var products = new List<Product>
        {
            new() { Id = "1", Name = "Oily Skin Cleanser", Brand = "Brand A", BasePrice = 19.99m }
        };

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("oily");
        _embeddingServiceMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), default)).ReturnsAsync(new float[768]);
        _vectorSearchMock.Setup(x => x.SearchSimilarProductsAsync(It.IsAny<float[]>(), 5, 0.7, default))
            .ReturnsAsync(products);
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "I have oily skin");

        Assert.Equal("oily", ctx.GetData<string>("skinType"));
        Assert.Contains("oily", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNoProducts_ShouldStillSaveSkinType()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinAnalysis };

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("sensitive");
        _embeddingServiceMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), default)).ReturnsAsync(new float[768]);
        _vectorSearchMock.Setup(x => x.SearchSimilarProductsAsync(It.IsAny<float[]>(), 5, 0.7, default))
            .ReturnsAsync(new List<Product>());
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "sensitive skin");

        Assert.Equal("sensitive", ctx.GetData<string>("skinType"));
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }
}
