using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class SkinConsultationStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IVectorSearchRepository> _vectorSearchRepositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<SkinConsultationStateHandler>> _loggerMock;
    private readonly SkinConsultationStateHandler _handler;

    public SkinConsultationStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _vectorSearchRepositoryMock = new Mock<IVectorSearchRepository>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<SkinConsultationStateHandler>>();
        _handler = new SkinConsultationStateHandler(
            _geminiServiceMock.Object,
            _vectorSearchRepositoryMock.Object,
            _embeddingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnSkinConsultation()
    {
        Assert.Equal(ConversationState.SkinConsultation, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithSkinConcern_ShouldProvideConsultation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinConsultation };
        var products = new List<Product>
        {
            new() { Id = "prod1", Name = "Product 1", BasePrice = 29.99m }
        };

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("Here's advice for your skin concern.");

        _embeddingServiceMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f });

        _vectorSearchRepositoryMock.Setup(x => x.SearchSimilarProductsAsync(
            It.IsAny<float[]>(),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var response = await _handler.HandleAsync(ctx, "I have dry skin");

        Assert.Contains("advice", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Product 1", response);
        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithMenuCommand_ShouldReturnToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinConsultation };

        var response = await _handler.HandleAsync(ctx, "menu");

        Assert.Contains("Main Menu", response);
        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
    }
}
