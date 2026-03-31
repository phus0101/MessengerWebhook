using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class SalesMessageParserTests
{
    private readonly Mock<IGeminiService> _mockGeminiService;

    public SalesMessageParserTests()
    {
        _mockGeminiService = new Mock<IGeminiService>();
    }
    [Theory]
    [InlineData("đúng rồi")]
    [InlineData("dung roi")]
    [InlineData("ok em")]
    [InlineData("oke em")]
    [InlineData("van dung")]
    [InlineData("len don")]
    [InlineData("chot don")]
    [InlineData("gui don")]
    [InlineData("nhu cu")]
    [InlineData("thong tin cu")]
    [InlineData("dia chi cu")]
    [InlineData("so cu")]
    [InlineData("cu nhu vay")]
    public async Task CaptureCustomerDetails_WithConfirmationKeywords_ShouldSetFlagToFalse(string message)
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);

        // Act
        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        // Assert
        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Theory]
    [InlineData("kem nay co tot khong?")]
    [InlineData("bao nhieu tien?")]
    [InlineData("ship bao lau?")]
    [InlineData("co mau khac khong?")]
    public async Task CaptureCustomerDetails_WithoutConfirmationKeywords_ShouldKeepFlagTrue(string message)
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);

        // Act
        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        // Assert
        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Fact]
    public void HasRequiredContact_WithAllInfoAndNoConfirmationNeeded_ShouldReturnTrue()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", false);

        // Act
        var result = SalesMessageParser.HasRequiredContact(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasRequiredContact_WithAllInfoButNeedsConfirmation_ShouldReturnFalse()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);

        // Act
        var result = SalesMessageParser.HasRequiredContact(context);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("0912345678")]
    [InlineData("0987654321")]
    [InlineData("84912345678")]
    [InlineData("091 234 5678")]
    [InlineData("091-234-5678")]
    public async Task CaptureCustomerDetails_WithPhoneNumber_ShouldExtractAndSetFlag(string message)
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("contactNeedsConfirmation", true);

        // Act
        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        // Assert
        Assert.NotNull(context.GetData<string>("customerPhone"));
        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Fact]
    public async Task CaptureCustomerDetails_WithAddress_ShouldExtractAndSetFlag()
    {
        // Arrange
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("contactNeedsConfirmation", true);

        // Act
        await SalesMessageParser.CaptureCustomerDetailsAsync(context, "123 Nguyen Trai, Quan 1, TPHCM", _mockGeminiService.Object, NullLogger.Instance);

        // Assert
        Assert.NotNull(context.GetData<string>("shippingAddress"));
        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
    }
}
