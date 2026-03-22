using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class IdleStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<IStateMachine> _stateMachineMock;
    private readonly Mock<ILogger<IdleStateHandler>> _loggerMock;
    private readonly IdleStateHandler _handler;

    public IdleStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _stateMachineMock = new Mock<IStateMachine>();
        _loggerMock = new Mock<ILogger<IdleStateHandler>>();
        _handler = new IdleStateHandler(_geminiServiceMock.Object, _stateMachineMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void HandledState_ShouldReturnIdle()
    {
        Assert.Equal(ConversationState.Idle, _handler.HandledState);
    }

    [Fact]
    public async Task HandleAsync_ShouldTransitionToGreeting()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Idle };
        _stateMachineMock.Setup(x => x.TransitionToAsync(ctx, ConversationState.Greeting)).ReturnsAsync(true);
        _stateMachineMock.Setup(x => x.SaveAsync(ctx)).Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(ctx, "hello");

        Assert.Contains("Welcome", response);
        _stateMachineMock.Verify(x => x.TransitionToAsync(ctx, ConversationState.Greeting), Times.Once);
        _stateMachineMock.Verify(x => x.SaveAsync(ctx), Times.Once);
    }
}
