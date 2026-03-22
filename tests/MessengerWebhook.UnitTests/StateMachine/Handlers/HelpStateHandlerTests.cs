using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class HelpStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<HelpStateHandler>> _loggerMock;
    private readonly HelpStateHandler _handler;

    public HelpStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<HelpStateHandler>>();
        _handler = new HelpStateHandler(_geminiServiceMock.Object, _stateMachineMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnHelp()
    {
        Assert.Equal(ConversationState.Help, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithPreviousState_ShouldReturnToPreviousState()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Help };
        ctx.SetData("previousState", ConversationState.BrowsingProducts);

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("Here's how to browse products...");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "how do I browse?");

        Assert.Contains("Returning", response);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.BrowsingProducts), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutPreviousState_ShouldReturnToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Help };

        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("I can help you with various features...");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.MainMenu)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "help");

        Assert.Contains("main menu", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.MainMenu), Times.Once);
    }
}
