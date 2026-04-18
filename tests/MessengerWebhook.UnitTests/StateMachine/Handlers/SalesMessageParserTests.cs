using MessengerWebhook.Models;
using Microsoft.Extensions.Logging;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ConversationHistoryMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class SalesMessageParserTests
{
    private readonly Mock<IGeminiService> _mockGeminiService;
    private readonly TestLogger _logger;

    public SalesMessageParserTests()
    {
        _mockGeminiService = new Mock<IGeminiService>();
        _logger = new TestLogger();
    }
    [Theory]
    [InlineData("đúng rồi")]
    [InlineData("dung roi")]
    [InlineData("van dung")]
    [InlineData("nhu cu")]
    [InlineData("thong tin cu")]
    [InlineData("dia chi cu")]
    [InlineData("so cu")]
    [InlineData("cu nhu vay")]
    public async Task CaptureCustomerDetails_WithConfirmationKeywords_ShouldSetFlagToFalse(string message)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");
        context.SetData("conversationHistory", new List<ConversationHistoryMessage>
        {
            new() { Role = "assistant", Content = "Chi xac nhan giup em SDT 0912345678 va dia chi 123 Test St con dung khong a?" }
        });

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Theory]
    [InlineData("kem nay co tot khong?")]
    [InlineData("bao nhieu tien?")]
    [InlineData("ship bao lau?")]
    [InlineData("co mau khac khong?")]
    [InlineData("ok em")]
    [InlineData("oke em")]
    [InlineData("len don")]
    [InlineData("chot don")]
    [InlineData("gui don")]
    public async Task CaptureCustomerDetails_WithoutConfirmationKeywords_ShouldKeepFlagTrue(string message)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");
        context.SetData("conversationHistory", new List<ConversationHistoryMessage>
        {
            new() { Role = "assistant", Content = "Da em tu van them cho chi a." }
        });

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("ok em")]
    [InlineData("oke em")]
    [InlineData("len don")]
    [InlineData("chot don")]
    [InlineData("chot nhe")]
    [InlineData("dat luon")]
    public async Task CaptureCustomerDetails_GenericBuyPhrases_AfterConfirmationPrompt_ShouldKeepFlagTrue(string message)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");
        context.SetData("conversationHistory", new List<ConversationHistoryMessage>
        {
            new() { Role = "assistant", Content = "Chi xac nhan giup em SDT 0912345678 va dia chi 123 Test St con dung khong a?" }
        });

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

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

    [Theory]
    [InlineData("123 Nguyen Trai, Quan 1, TPHCM")]
    [InlineData("12 Trần Hưng Đạo, Quận 1, TP.HCM")]
    public async Task CaptureCustomerDetails_WithAddress_ShouldExtractAndSetFlag(string message)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("contactNeedsConfirmation", true);

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        Assert.NotNull(context.GetData<string>("shippingAddress"));
        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
    }

    [Theory]
    [InlineData("có freeship ko e")]
    [InlineData("ship về quận 1 mất bao lâu vậy em?")]
    public async Task CaptureCustomerDetails_WithShippingQuestion_ShouldNotTreatShipAsAddress(string message)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.Consulting
        };
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("rememberedCustomerPhone", "0912345678");
        context.SetData("rememberedShippingAddress", "123 Test St");
        context.SetData("pendingContactQuestion", "confirm_old_contact");

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, message, _mockGeminiService.Object, NullLogger.Instance);

        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("123 Test St", context.GetData<string>("shippingAddress"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));
    }

    [Fact]
    public async Task CaptureCustomerDetails_WithNewContactDuringRememberedConfirmation_ShouldMarkCurrentOrderAsUpdated()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("rememberedCustomerPhone", "0912345678");
        context.SetData("rememberedShippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");

        await SalesMessageParser.CaptureCustomerDetailsAsync(
            context,
            "Số mới của chị là 0988888888, địa chỉ mới là 99 Le Loi quan 3",
            _mockGeminiService.Object,
            NullLogger.Instance);

        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("ask_save_new_contact", context.GetData<string>("pendingContactQuestion"));
        Assert.True(context.GetData<bool?>("currentOrderUsesUpdatedContact"));
        Assert.False(context.GetData<bool?>("saveCurrentContactForFuture"));
    }

    [Fact]
    public async Task CaptureCustomerDetails_WithSameRememberedContact_ShouldNotMarkCurrentOrderAsUpdated()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("rememberedCustomerPhone", "0912345678");
        context.SetData("rememberedShippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");

        await SalesMessageParser.CaptureCustomerDetailsAsync(
            context,
            "Số của chị là 0912345678, địa chỉ 123 Test St",
            _mockGeminiService.Object,
            NullLogger.Instance);

        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));
        Assert.NotEqual(true, context.GetData<bool?>("currentOrderUsesUpdatedContact"));
    }

    [Fact]
    public void CaptureSelectedProductQuantity_WithExplicitOrderQuantity_PersistsQuantityMap()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("selectedProductCodes", new List<string> { "MN" });

        SalesMessageParser.CaptureSelectedProductQuantity(context, "ok vậy chị chốt 2 sản phẩm mặt nạ ngủ nhé");

        var quantities = context.GetData<Dictionary<string, int>>("selectedProductQuantities");
        Assert.NotNull(quantities);
        Assert.Equal(2, quantities!["MN"]);
    }

    [Fact]
    public void CaptureSelectedProductQuantity_WithMultipleProducts_UsesLatestSelectedProductOnly()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("selectedProductCodes", new List<string> { "KCN", "MN" });

        SalesMessageParser.CaptureSelectedProductQuantity(context, "ok vậy chị chốt 2 sản phẩm mặt nạ ngủ nhé");

        var quantities = context.GetData<Dictionary<string, int>>("selectedProductQuantities");
        Assert.NotNull(quantities);
        Assert.False(quantities!.ContainsKey("KCN"));
        Assert.Equal(2, quantities["MN"]);
    }

    [Fact]
    public void CaptureSelectedProductQuantity_WithPhoneOrAddressMessage_DoesNotTreatNumbersAsQuantity()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("selectedProductCodes", new List<string> { "MN" });

        SalesMessageParser.CaptureSelectedProductQuantity(context, "Số của chị là 0901234567, địa chỉ 12 Trần Hưng Đạo quận 1");

        var quantities = context.GetData<Dictionary<string, int>>("selectedProductQuantities");
        Assert.True(quantities == null || quantities.Count == 0);
    }

    [Fact]
    public void CaptureSelectedProductQuantity_WithShippingFeeQuestion_DoesNotTreatPriceAsQuantity()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("selectedProductCodes", new List<string> { "MN" });

        SalesMessageParser.CaptureSelectedProductQuantity(context, "phí ship 30.000đ thì sao em");

        var quantities = context.GetData<Dictionary<string, int>>("selectedProductQuantities");
        Assert.True(quantities == null || quantities.Count == 0);
    }

    [Fact]
    public async Task CaptureCustomerDetails_WithPartialAiExtractionDuringRememberedConfirmation_ShouldKeepConfirmationFlagTrue()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("rememberedCustomerPhone", "0912345678");
        context.SetData("rememberedShippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");

        _mockGeminiService
            .Setup(x => x.SendMessageAsync(
                "system",
                It.IsAny<string>(),
                It.IsAny<List<ConversationHistoryMessage>>(),
                GeminiModelType.FlashLite,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"Phone\":\"0988888888\",\"Address\":null}");

        await SalesMessageParser.CaptureCustomerDetailsAsync(
            context,
            "chị muốn cập nhật sdt mới nha",
            _mockGeminiService.Object,
            _logger);

        _mockGeminiService.Verify(x => x.SendMessageAsync(
            "system",
            It.IsAny<string>(),
            It.IsAny<List<ConversationHistoryMessage>>(),
            GeminiModelType.FlashLite,
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.True(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Equal("0912345678", context.GetData<string>("customerPhone"));
        Assert.Equal("123 Test St", context.GetData<string>("shippingAddress"));
        Assert.Equal("confirm_old_contact", context.GetData<string>("pendingContactQuestion"));
    }

    [Theory]
    [InlineData("{\"Phone\":\"0988888888\",\"Address\":\"99 Le Loi quan 3\"}")]
    [InlineData("{\"phone\":\"0988888888\",\"address\":\"99 Le Loi quan 3\"}")]
    public async Task CaptureCustomerDetails_WithAiExtractionLogging_ShouldNotLogRawPhoneOrAddress(string aiResponse)
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };

        _mockGeminiService
            .Setup(x => x.SendMessageAsync(
                "system",
                It.IsAny<string>(),
                It.IsAny<List<ConversationHistoryMessage>>(),
                GeminiModelType.FlashLite,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiResponse);

        await SalesMessageParser.CaptureCustomerDetailsAsync(
            context,
            "chị cập nhật sđt mới giúp em nha",
            _mockGeminiService.Object,
            _logger);

        _mockGeminiService.Verify(x => x.SendMessageAsync(
            "system",
            It.IsAny<string>(),
            It.IsAny<List<ConversationHistoryMessage>>(),
            GeminiModelType.FlashLite,
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("0988888888", context.GetData<string>("customerPhone"));
        Assert.Equal("99 Le Loi quan 3", context.GetData<string>("shippingAddress"));

        var combinedLogs = string.Join("\n", _logger.Messages);
        Assert.DoesNotContain("0988888888", combinedLogs);
        Assert.DoesNotContain("99 Le Loi quan 3", combinedLogs);
        Assert.Contains("Method=ai-extraction", combinedLogs);
    }

    [Fact]
    public async Task CaptureCustomerDetails_WithConfirmationKeywordsAndNoHistory_ShouldStillConfirmWhenPendingQuestionExists()
    {
        var context = new StateContext
        {
            FacebookPSID = "test-psid",
            CurrentState = ConversationState.CollectingInfo
        };
        context.SetData("customerPhone", "0912345678");
        context.SetData("shippingAddress", "123 Test St");
        context.SetData("contactNeedsConfirmation", true);
        context.SetData("pendingContactQuestion", "confirm_old_contact");

        await SalesMessageParser.CaptureCustomerDetailsAsync(context, "nhu cu", _mockGeminiService.Object, NullLogger.Instance);

        Assert.False(context.GetData<bool?>("contactNeedsConfirmation"));
        Assert.Null(context.GetData<string>("pendingContactQuestion"));
    }

    private sealed class TestLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
