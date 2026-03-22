using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class GreetingStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<GreetingStateHandler>> _loggerMock;
    private readonly GreetingStateHandler _handler;

    public GreetingStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<GreetingStateHandler>>();
        _handler = new GreetingStateHandler(_geminiServiceMock.Object, _stateMachineMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnGreeting()
    {
        Assert.Equal(ConversationState.Greeting, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_WithSkinIntent_ShouldTransitionToSkinConsultation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Greeting };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("skin_analysis");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.SkinConsultation)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "I need skin consultation");

        Assert.Contains("consultation", response, StringComparison.OrdinalIgnoreCase);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.SkinConsultation), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDefaultIntent_ShouldTransitionToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Greeting };
        _geminiServiceMock.Setup(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default)).ReturnsAsync("greeting");
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.MainMenu)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "hi");

        Assert.Contains("Hello", response);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.MainMenu), Times.Once);
    }
}
