using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class IdleStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<IdleStateHandler>> _loggerMock;
    private readonly IdleStateHandler _handler;

    public IdleStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<IdleStateHandler>>();
        _handler = new IdleStateHandler(_geminiServiceMock.Object, _loggerMock.Object);
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

        var response = await _handler.HandleAsync(ctx, "hello");

        Assert.Contains("Welcome", response);
        Assert.Equal(ConversationState.Greeting, ctx.CurrentState);
    }
}
