using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class MainMenuStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<MainMenuStateHandler>> _loggerMock;
    private readonly MainMenuStateHandler _handler;

    public MainMenuStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<MainMenuStateHandler>>();
        _handler = new MainMenuStateHandler(_geminiServiceMock.Object, _stateMachineMock.Object, _loggerMock.Object);
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
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "browse products");

        Assert.Contains("products", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
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
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.Help)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "help");

        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.Help), Times.Once);
        Assert.Equal(ConversationState.MainMenu, ctx.GetData<ConversationState?>("previousState"));
    }
}
