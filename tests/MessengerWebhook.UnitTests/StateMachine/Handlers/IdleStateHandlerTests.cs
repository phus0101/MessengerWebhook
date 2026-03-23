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
    public async Task HandleAsync_WithSkinConsultationIntent_ShouldTransitionToSkinConsultation()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Idle };

        _geminiServiceMock.SetupSequence(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default))
            .ReturnsAsync("skin_consultation")
            .ReturnsAsync("Chào bạn! Tôi hiểu bạn đang cần tư vấn về sản phẩm cho da dầu.");

        var response = await _handler.HandleAsync(ctx, "tôi muốn tư vấn sản phẩm cho da dầu");

        Assert.Equal(ConversationState.SkinConsultation, ctx.CurrentState);
        Assert.Contains("tư vấn", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WithBrowseProductsIntent_ShouldTransitionToBrowsingProducts()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Idle };

        _geminiServiceMock.SetupSequence(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default))
            .ReturnsAsync("browse_products")
            .ReturnsAsync("Chào bạn! Tôi sẽ giúp bạn xem các sản phẩm của shop.");

        var response = await _handler.HandleAsync(ctx, "cho tôi xem sản phẩm");

        Assert.Equal(ConversationState.BrowsingProducts, ctx.CurrentState);
        Assert.Contains("sản phẩm", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WithOrderTrackingIntent_ShouldTransitionToOrderTracking()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Idle };

        _geminiServiceMock.SetupSequence(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default))
            .ReturnsAsync("order_tracking")
            .ReturnsAsync("Chào bạn! Tôi sẽ giúp bạn kiểm tra đơn hàng.");

        var response = await _handler.HandleAsync(ctx, "kiểm tra đơn hàng của tôi");

        Assert.Equal(ConversationState.OrderTracking, ctx.CurrentState);
        Assert.Contains("đơn hàng", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WithGreetingIntent_ShouldTransitionToMainMenu()
    {
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Idle };

        _geminiServiceMock.SetupSequence(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            null,
            default))
            .ReturnsAsync("greeting")
            .ReturnsAsync("Xin chào! Rất vui được hỗ trợ bạn.");

        var response = await _handler.HandleAsync(ctx, "xin chào");

        Assert.Equal(ConversationState.MainMenu, ctx.CurrentState);
        Assert.Contains("Bạn muốn:", response);
    }
}
