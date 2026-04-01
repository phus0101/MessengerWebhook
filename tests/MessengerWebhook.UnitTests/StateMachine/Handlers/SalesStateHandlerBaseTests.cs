using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class SalesStateHandlerBaseTests
{
    private readonly Mock<IGeminiService> _geminiService;
    private readonly Mock<ICustomerIntelligenceService> _customerService;
    private readonly Mock<IDraftOrderService> _draftOrderService;
    private readonly SalesBotOptions _salesBotOptions;
    private readonly TestSalesStateHandler _handler;

    public SalesStateHandlerBaseTests()
    {
        _geminiService = new Mock<IGeminiService>();
        _customerService = new Mock<ICustomerIntelligenceService>();
        _draftOrderService = new Mock<IDraftOrderService>();

        _salesBotOptions = new SalesBotOptions
        {
            MaxConsultationAttempts = 2,
            ConversationHistoryLimit = 15,
            IntentConfidenceThreshold = 0.7
        };

        _customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        _customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });

        _handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _draftOrderService.Object,
            _customerService.Object,
            Options.Create(_salesBotOptions),
            Mock.Of<ILogger<TestSalesStateHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ShouldTrackConsultationRejection_WhenCustomerDeclinesAfterConsultationQuestion()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm về sản phẩm không ạ?", Timestamp = DateTime.UtcNow }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                default))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.ReadyToBuy,
                Confidence = 0.9,
                Reason = "Customer declined consultation"
            });

        // Act
        await _handler.HandleAsync(ctx, "Không cần, em lên đơn luôn");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        Assert.Equal(1, rejectionCount);
    }

    [Fact]
    public async Task HandleAsync_ShouldAutoClose_AfterMaxConsultationRejections()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("consultationRejectionCount", 1); // Already rejected once
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm không ạ?", Timestamp = DateTime.UtcNow }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                default))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.ReadyToBuy,
                Confidence = 0.9,
                Reason = "Customer declined consultation again"
            });

        _draftOrderService
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder { Id = Guid.NewGuid(), DraftCode = "DR-TEST-001" });

        // Act
        var response = await _handler.HandleAsync(ctx, "Không, em lên đơn luôn");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        Assert.Equal(2, rejectionCount);
        Assert.Contains("lên đơn", response.ToLower());
    }

    [Fact]
    public async Task HandleAsync_ShouldEnforceConversationHistoryLimit()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

        // Add 20 messages (exceeds limit of 15)
        var history = new List<AiConversationMessage>();
        for (int i = 0; i < 20; i++)
        {
            history.Add(new AiConversationMessage
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"Message {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(-20 + i)
            });
        }
        ctx.SetData("conversationHistory", history);

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                default))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.Consulting,
                Confidence = 0.8,
                Reason = "Customer asking question"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                null,
                default))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        // Act
        await _handler.HandleAsync(ctx, "Cho em hỏi về sản phẩm");

        // Assert
        var updatedHistory = ctx.GetData<List<AiConversationMessage>>("conversationHistory");
        Assert.True(updatedHistory.Count <= _salesBotOptions.ConversationHistoryLimit,
            $"History count {updatedHistory.Count} exceeds limit {_salesBotOptions.ConversationHistoryLimit}");
    }

    [Fact]
    public async Task HandleAsync_ShouldNotTrackRejection_WhenNotAfterConsultationQuestion()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Dạ sản phẩm này có giá 350k ạ", Timestamp = DateTime.UtcNow }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                default))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.ReadyToBuy,
                Confidence = 0.9,
                Reason = "Customer ready to buy"
            });

        // Act
        await _handler.HandleAsync(ctx, "Ok em lên đơn luôn");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        Assert.Equal(0, rejectionCount);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotTrackRejection_WhenConfidenceBelowThreshold()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm không ạ?", Timestamp = DateTime.UtcNow }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                default))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.ReadyToBuy,
                Confidence = 0.5, // Below threshold
                Reason = "Low confidence"
            });

        // Act
        await _handler.HandleAsync(ctx, "không");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        Assert.Equal(0, rejectionCount);
    }

    // Test handler implementation
    private class TestSalesStateHandler : SalesStateHandlerBase
    {
        public override ConversationState HandledState => ConversationState.Consulting;

        public TestSalesStateHandler(
            IGeminiService geminiService,
            IPolicyGuardService policyGuardService,
            IProductMappingService productMappingService,
            IGiftSelectionService giftSelectionService,
            IFreeshipCalculator freeshipCalculator,
            ICaseEscalationService caseEscalationService,
            IDraftOrderService draftOrderService,
            ICustomerIntelligenceService customerIntelligenceService,
            IOptions<SalesBotOptions> salesBotOptions,
            ILogger<TestSalesStateHandler> logger)
            : base(
                geminiService,
                policyGuardService,
                productMappingService,
                giftSelectionService,
                freeshipCalculator,
                caseEscalationService,
                draftOrderService,
                customerIntelligenceService,
                salesBotOptions,
                logger)
        {
        }

        protected override Task<string> HandleInternalAsync(StateContext ctx, string message)
        {
            return Task.FromResult("Test response");
        }
    }
}
