using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Support;
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
            .Setup(x => x.GetProductByCodeAsync("MN"))
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

        var handler = new DraftOrderStateHandler(
            Mock.Of<IGeminiService>(),
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            new DraftOrderCoordinator(draftOrderService.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            customerService.Object,
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<DraftOrderStateHandler>>());

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
}
