using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class HelpStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiServiceMock;
    private readonly Mock<ILogger<HelpStateHandler>> _loggerMock;
    private readonly HelpStateHandler _handler;

    public HelpStateHandlerTests()
    {
        _geminiServiceMock = new Mock<IGeminiService>();
        _loggerMock = new Mock<ILogger<HelpStateHandler>>();
        _handler = new HelpStateHandler(_geminiServiceMock.Object, _loggerMock.Object);
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

        var response = await _handler.HandleAsync(ctx, "how do I browse?");

        Assert.Contains("Returning", response);
        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
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

        var response = await _handler.HandleAsync(ctx, "help");

        Assert.Contains("main menu", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
    }
}
