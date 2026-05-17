using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Intent;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.AI.Resilience;
using MessengerWebhook.Services.Cache;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class DraftOrderStateHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldUseUnifiedDraftConfirmation()
    {
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());

        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });

        var giftSelectionService = new Mock<IGiftSelectionService>();
        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Mat na mini phuc hoi" });

        var draftOrderService = new Mock<IDraftOrderService>();
        draftOrderService
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder
            {
                Id = Guid.NewGuid(),
                DraftCode = "DR-TEST-DRAFTORDER",
                MerchandiseTotal = 250000m,
                ShippingFee = 30000m,
                GrandTotal = 280000m,
                Items = new List<DraftOrderItem>
                {
                    new() { ProductCode = "MN", ProductName = "Mat Na Ngu", Quantity = 1, UnitPrice = 250000m }
                }
            });

        var geminiService = Mock.Of<IGeminiService>();
        var salesBotOptions = Options.Create(new SalesBotOptions());
        var ragOptions = Options.Create(new RAGOptions { Enabled = false });
        var groundingService = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector());
        var contextResolver = new SalesContextResolver(
            customerService.Object, productMappingService.Object, giftSelectionService.Object,
            new FreeshipCalculator(), groundingService, geminiService,
            NullLogger<SalesContextResolver>.Instance);
        var promptBuilder = new SalesPromptBuilder();
        var contactFlow = new ContactConfirmationFlow(contextResolver, promptBuilder);
        var replyOrchestrator = new SalesReplyOrchestrator(
            geminiService, Mock.Of<MessengerWebhook.Services.AI.Routing.ILlmRoutingService>(), null,
            Mock.Of<IEmotionDetectionService>(), Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(), Mock.Of<IConversationMetricsService>(),
            customerService.Object, groundingService, contextResolver, promptBuilder,
            Mock.Of<ISemanticAnswerCache>(), Mock.Of<ITenantContext>(),
            salesBotOptions, ragOptions,
            NullLogger<SalesReplyOrchestrator>.Instance);
        var consultationReplies = new SalesConsultationReplies(
            contextResolver, promptBuilder, productMappingService.Object,
            NullLogger<SalesConsultationReplies>.Instance);

        var handler = new DraftOrderStateHandler(
            geminiService,
            new PolicyGuardService(salesBotOptions),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(draftOrderService.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
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
            salesBotOptions,
            Options.Create(new PolicyGuardOptions()),
            ragOptions,
            Mock.Of<ILogger<DraftOrderStateHandler>>(),
            contextResolver,
            promptBuilder,
            contactFlow,
            replyOrchestrator,
            consultationReplies,
            Mock.Of<ILlmFallbackService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationSummarizer>(),
            new CommerceMsgIntentDetector(Mock.Of<IContactConfirmationFlow>(), Mock.Of<ISalesContextResolver>()));

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.DraftOrder, SessionId = "session-1" };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });
        ctx.SetData("selectedProductQuantities", new Dictionary<string, int> { ["MN"] = 1 });
        ctx.SetData("customerPhone", "0901234567");
        ctx.SetData("shippingAddress", "12 Tran Hung Dao");

        var response = await handler.HandleAsync(ctx, "ok em");

        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
        Assert.Contains("DR-TEST-DRAFTORDER", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("250,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phí ship và tổng đơn cuối em sẽ kiểm tra lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("280,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mat na mini phuc hoi", response, StringComparison.OrdinalIgnoreCase);
    }

    // ── NEW TESTS (coverage bridging) ────────────────────────────────────────

    // A. HandledState property
    [Fact]
    public void HandledState_ShouldReturnDraftOrder()
    {
        var handler = new DraftOrderStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), Mock.Of<IMemoryCache>(), NullLogger<DraftOrderCoordinator>.Instance),
            Mock.Of<ICustomerIntelligenceService>(),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new PolicyGuardOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<DraftOrderStateHandler>>(),
            Mock.Of<ISalesContextResolver>(),
            Mock.Of<ISalesPromptBuilder>(),
            Mock.Of<IContactConfirmationFlow>(),
            Mock.Of<ISalesReplyOrchestrator>(),
            Mock.Of<ISalesConsultationReplies>(),
            Mock.Of<ILlmFallbackService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationSummarizer>(),
            new CommerceMsgIntentDetector(Mock.Of<IContactConfirmationFlow>(), Mock.Of<ISalesContextResolver>()));

        Assert.Equal(ConversationState.DraftOrder, handler.HandledState);
    }

    // B. No product selected → TryCreateDraftConfirmationAsync returns empty → transitions to Complete
    [Fact]
    public async Task HandleAsync_WhenNoProductSelected_ShouldTransitionToComplete()
    {
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());

        var handler = new DraftOrderStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(Mock.Of<IDraftOrderService>(), new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            customerService.Object,
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new PolicyGuardOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<DraftOrderStateHandler>>(),
            Mock.Of<ISalesContextResolver>(),
            Mock.Of<ISalesPromptBuilder>(),
            Mock.Of<IContactConfirmationFlow>(),
            Mock.Of<ISalesReplyOrchestrator>(),
            Mock.Of<ISalesConsultationReplies>(),
            Mock.Of<ILlmFallbackService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationSummarizer>(),
            new CommerceMsgIntentDetector(Mock.Of<IContactConfirmationFlow>(), Mock.Of<ISalesContextResolver>()));

        // No selectedProductCodes set → confirmation empty → fallthrough
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.DraftOrder };

        var response = await handler.HandleAsync(ctx, "ok");

        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
        Assert.Contains("lên đơn nháp", response, StringComparison.OrdinalIgnoreCase);
    }

    // C. DraftOrder handler: when draft coordinator returns a draft → state transitions to Complete with confirmation
    [Fact]
    public async Task HandleAsync_WhenDraftOrderCreatedSuccessfully_ShouldReturnConfirmationAndCompleteState()
    {
        var customerService = new Mock<ICustomerIntelligenceService>();
        customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());

        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });

        var draftOrderService = new Mock<IDraftOrderService>();
        draftOrderService
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder
            {
                Id = Guid.NewGuid(),
                DraftCode = "DR-TEST-POLICY",
                MerchandiseTotal = 320000m,
                ShippingFee = 30000m,
                GrandTotal = 350000m,
                Items = new List<DraftOrderItem>
                {
                    new() { ProductCode = "KCN", ProductName = "Kem Chong Nang", Quantity = 1, UnitPrice = 320000m }
                }
            });

        var geminiService2 = Mock.Of<IGeminiService>();
        var salesBotOptions2 = Options.Create(new SalesBotOptions());
        var ragOptions2 = Options.Create(new RAGOptions { Enabled = false });
        var groundingService2 = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector());
        var contextResolver2 = new SalesContextResolver(
            customerService.Object, productMappingService.Object, Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(), groundingService2, geminiService2,
            NullLogger<SalesContextResolver>.Instance);
        var promptBuilder2 = new SalesPromptBuilder();
        var contactFlow2 = new ContactConfirmationFlow(contextResolver2, promptBuilder2);
        var replyOrchestrator2 = new SalesReplyOrchestrator(
            geminiService2, Mock.Of<MessengerWebhook.Services.AI.Routing.ILlmRoutingService>(), null,
            Mock.Of<IEmotionDetectionService>(), Mock.Of<IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(), Mock.Of<IConversationMetricsService>(),
            customerService.Object, groundingService2, contextResolver2, promptBuilder2,
            Mock.Of<ISemanticAnswerCache>(), Mock.Of<ITenantContext>(),
            salesBotOptions2, ragOptions2,
            NullLogger<SalesReplyOrchestrator>.Instance);
        var consultationReplies2 = new SalesConsultationReplies(
            contextResolver2, promptBuilder2, productMappingService.Object,
            NullLogger<SalesConsultationReplies>.Instance);

        var handler = new DraftOrderStateHandler(
            geminiService2,
            new PolicyGuardService(salesBotOptions2),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(draftOrderService.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
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
            salesBotOptions2,
            Options.Create(new PolicyGuardOptions()),
            ragOptions2,
            Mock.Of<ILogger<DraftOrderStateHandler>>(),
            contextResolver2,
            promptBuilder2,
            contactFlow2,
            replyOrchestrator2,
            consultationReplies2,
            Mock.Of<ILlmFallbackService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationSummarizer>(),
            new CommerceMsgIntentDetector(Mock.Of<IContactConfirmationFlow>(), Mock.Of<ISalesContextResolver>()));

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.DraftOrder, SessionId = "session-1" };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("selectedProductQuantities", new Dictionary<string, int> { ["KCN"] = 1 });
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "12 Le Loi");

        var response = await handler.HandleAsync(ctx, "ok lên đơn nhé");

        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
        Assert.Contains("DR-TEST-POLICY", response, StringComparison.OrdinalIgnoreCase);
    }
}
