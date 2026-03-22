using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class MainMenuStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<MainMenuStateHandler>> _loggerMock;
    private readonly MainMenuStateHandler _handler;

    public MainMenuStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<MainMenuStateHandler>>();
        _handler = new MainMenuStateHandler(_geminiServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnMainMenu()
    {
        Assert.Equal(ConversationState.MainMenu, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithBrowseIntent_ShouldTransitionToBrowsingProducts()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.MainMenu };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("browse_products");

        var response = await _handler.HandleAsync(ctx, "browse products");

        Assert.Contains("products", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
    }

    [Fact]
    public async Task HandleAsync_WithHelpIntent_ShouldTransitionToHelp()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.MainMenu };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("help");

        var response = await _handler.HandleAsync(ctx, "help");

        Assert.Equal(ConversationState.Help, ctx.CurrentState);
        Assert.Equal(ConversationState.MainMenu, ctx.GetData<ConversationState?>("previousState"));
    }
}
