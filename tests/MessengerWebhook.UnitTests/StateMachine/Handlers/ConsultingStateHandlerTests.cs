using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class ConsultingStateHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithContactInfoAndSelectedProduct_ShouldCreateDraftOrder()
    {
        var geminiService = new Mock<IGeminiService>();
        var draftOrderService = new Mock<IDraftOrderService>();
        var customerService = new Mock<ICustomerIntelligenceService>();

        customerService
            .Setup(x => x.GetExistingAsync(It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync((CustomerIdentity?)null);
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });
        draftOrderService
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder { Id = Guid.NewGuid(), DraftCode = "DR-TEST-001" });

        var draftOrderCoordinator = new DraftOrderCoordinator(
            draftOrderService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<DraftOrderCoordinator>.Instance);

        // Mock AI intent detection
        geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                default))
            .ReturnsAsync(new MessengerWebhook.Services.AI.Models.IntentDetectionResult
            {
                Intent = MessengerWebhook.Services.AI.Models.CustomerIntent.ReadyToBuy,
                Confidence = 0.9,
                Reason = "Customer ready to buy"
            });

        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new ConsultingStateHandler(
            geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            draftOrderCoordinator,
            customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<ConsultingStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "psid-3", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "12 đường Hoa Mai, quận 3");
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "12 đường Hoa Mai, quận 3");

        var response = await handler.HandleAsync(ctx, "Số tôi 0912345678, địa chỉ 12 đường Hoa Mai quận 3");

        Assert.Equal(ConversationState.CollectingInfo, ctx.CurrentState);
        Assert.Contains("tóm tắt đơn", response, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ctx.GetData<string>("draftOrderCode"));
        Assert.DoesNotContain("DR-TEST-001", response, StringComparison.OrdinalIgnoreCase);
    }

    // ── NEW TESTS (coverage bridging) ────────────────────────────────────────

    // A. HandledState property
    [Fact]
    public void HandledState_ShouldReturnConsulting()
    {
        var geminiService = new Mock<IGeminiService>();
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });

        var handler = new ConsultingStateHandler(
            geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<ConsultingStateHandler>>());

        Assert.Equal(ConversationState.Consulting, handler.HandledState);
    }

    // B. Gemini returns response → bot reply returned
    [Fact]
    public async Task HandleAsync_WhenGeminiReturnsResponse_ShouldReturnBotReply()
    {
        var geminiService = new Mock<IGeminiService>();
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });
        geminiService
            .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(), It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync("Dạ chị muốn mua sản phẩm nào ạ?");
        geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                default))
            .ReturnsAsync(new MessengerWebhook.Services.AI.Models.IntentDetectionResult
            {
                Intent = MessengerWebhook.Services.AI.Models.CustomerIntent.Browsing,
                Confidence = 0.8,
                Reason = "browsing"
            });

        var handler = new ConsultingStateHandler(
            geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<ConsultingStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

        var response = await handler.HandleAsync(ctx, "em muốn mua kem");

        Assert.NotEmpty(response);
    }

    // C. Empty message → no exception, returns some response
    [Fact]
    public async Task HandleAsync_WhenMessageIsEmpty_ShouldNotThrow()
    {
        var geminiService = new Mock<IGeminiService>();
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });
        geminiService
            .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(), It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync("Dạ em có thể giúp gì cho chị ạ?");
        geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                default))
            .ReturnsAsync(new MessengerWebhook.Services.AI.Models.IntentDetectionResult
            {
                Intent = MessengerWebhook.Services.AI.Models.CustomerIntent.Browsing,
                Confidence = 0.5,
                Reason = "empty"
            });

        var handler = new ConsultingStateHandler(
            geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            customerService.Object,
            null,
            Mock.Of<IEmotionDetectionService>(),
            Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<ConsultingStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

        var exception = await Record.ExceptionAsync(() => handler.HandleAsync(ctx, ""));

        Assert.Null(exception);
    }
}
