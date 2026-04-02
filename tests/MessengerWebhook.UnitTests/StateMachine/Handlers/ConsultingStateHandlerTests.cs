using MessengerWebhook.Models;
using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
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
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });
        draftOrderService
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder { Id = Guid.NewGuid(), DraftCode = "DR-TEST-001" });

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

        var handler = new ConsultingStateHandler(
            geminiService.Object,
            new PolicyGuardService(Options.Create(new SalesBotOptions())),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            draftOrderService.Object,
            customerService.Object,
            null,
            Options.Create(new SalesBotOptions()),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<ConsultingStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "psid-3", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });

        var response = await handler.HandleAsync(ctx, "Số tôi 0912345678, địa chỉ 12 đường Hoa Mai quận 3");

        Assert.Equal(ConversationState.Complete, ctx.CurrentState);
        Assert.Equal("DR-TEST-001", ctx.GetData<string>("draftOrderCode"));
        Assert.Contains("DR-TEST-001", response);
    }
}
