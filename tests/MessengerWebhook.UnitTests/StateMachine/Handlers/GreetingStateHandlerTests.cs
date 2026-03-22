using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class GreetingStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<GreetingStateHandler>> _loggerMock;
    private readonly GreetingStateHandler _handler;

    public GreetingStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<GreetingStateHandler>>();
        _handler = new GreetingStateHandler(_geminiServiceMock.Object, _loggerMock.Object);
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

        var response = await _handler.HandleAsync(ctx, "I need skin consultation");

        Assert.Contains("consultation", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.SkinConsultation, ctx.CurrentState);
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

        var response = await _handler.HandleAsync(ctx, "hi");

        Assert.Contains("Hello", response);
        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
    }
}
