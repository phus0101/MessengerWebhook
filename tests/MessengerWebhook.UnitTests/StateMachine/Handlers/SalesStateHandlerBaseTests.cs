using MessengerWebhook.Configuration;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Models;
using MessengerWebhook.Services;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Tenants;
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
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class SalesStateHandlerBaseTests
{
    private readonly Mock<IGeminiService> _geminiService;
    private readonly Mock<ICustomerIntelligenceService> _customerService;
    private readonly SalesBotOptions _salesBotOptions;
    private readonly Mock<IDraftOrderService> _draftOrderServiceMock;
    private readonly TestSalesStateHandler _handler;

    public SalesStateHandlerBaseTests()
    {
        _geminiService = new Mock<IGeminiService>();
        _customerService = new Mock<ICustomerIntelligenceService>();
        _draftOrderServiceMock = new Mock<IDraftOrderService>();

        _salesBotOptions = new SalesBotOptions
        {
            MaxConsultationAttempts = 2,
            ConversationHistoryLimit = 15,
            IntentConfidenceThreshold = 0.7
        };

        _customerService
            .Setup(x => x.GetExistingAsync(It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync((CustomerIdentity?)null);
        _customerService
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new CustomerIdentity());
        _customerService
            .Setup(x => x.GetVipProfileAsync(It.IsAny<CustomerIdentity>(), default))
            .ReturnsAsync(new VipProfile { GreetingStyle = string.Empty });

        var draftOrderCoordinator = new DraftOrderCoordinator(
            _draftOrderServiceMock.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<DraftOrderCoordinator>.Instance);

        _handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            draftOrderCoordinator,
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ShouldTrackConsultationRejection_WhenCustomerDeclinesAfterConsultationQuestion()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });

        // Set history with bot's consultation question as the last message
        // Note: HandleSalesConversationAsync will add user message first, so bot message should be before that
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm về sản phẩm không ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
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

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

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

        // Set history with bot's consultation question
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm không ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
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

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        _draftOrderServiceMock
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
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        // Act
        await _handler.HandleAsync(ctx, "Cho em hỏi về sản phẩm");

        // Assert
        var updatedHistory = ctx.GetData<List<AiConversationMessage>>("conversationHistory");
        Assert.NotNull(updatedHistory);
        var persistedHistory = updatedHistory!;
        Assert.True(persistedHistory.Count <= _salesBotOptions.ConversationHistoryLimit,
            $"History count {persistedHistory.Count} exceeds limit {_salesBotOptions.ConversationHistoryLimit}");
    }

    [Fact]
    public async Task HandleAsync_ShouldNotTrackRejection_WhenNotAfterConsultationQuestion()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });

        // Set history with bot message that is NOT a consultation question
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Giá bao nhiêu?", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ sản phẩm này có giá 350k ạ", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
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

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

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

        // Set history with bot's consultation question
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Cho em xem sản phẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Chị cần tư vấn thêm không ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
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

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        // Act
        await _handler.HandleAsync(ctx, "không");

        // Assert
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        Assert.Equal(0, rejectionCount);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotCreateDraft_WhenCustomerIsQuestioningAboutShipping()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(new Gift { Code = "GIFT_KCN", Name = "Mat na duong sang" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "12 Tran Hung Dao");
        ctx.SetData("contactNeedsConfirmation", false);

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer is asking about shipping policy"
            });

        var response = await handler.HandleAsync(ctx, "Có freeship không em?");

        Assert.Contains("chưa dám chốt freeship", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dữ liệu nội bộ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lên đơn", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("len don", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("don nhap", response, StringComparison.OrdinalIgnoreCase);
        _draftOrderServiceMock.Verify(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateDraft_WhenReturningCustomerConfirmsRememberedContact()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });
        ctx.SetData("customerPhone", "0987654321");
        ctx.SetData("shippingAddress", "456 Another Street, District 3, HCMC");
        ctx.SetData("contactNeedsConfirmation", false);

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

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
                Confidence = 0.95,
                Reason = "Customer confirmed remembered contact"
            });

        _draftOrderServiceMock
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder
            {
                Id = Guid.NewGuid(),
                DraftCode = "DR-TEST-RETURNING",
                MerchandiseTotal = 320000m,
                ShippingFee = 30000m,
                GrandTotal = 350000m,
                Items = new List<DraftOrderItem>
                {
                    new() { ProductCode = "KCN", ProductName = "Kem Chong Nang", Quantity = 1, UnitPrice = 320000m }
                }
            });

        var response = await handler.HandleAsync(ctx, "ok em");

        Assert.Contains("tóm tắt đơn", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DR-TEST-RETURNING", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("320,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phí ship: em cần kiểm tra lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tổng đơn cuối: em sẽ báo lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("350,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xác nhận lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xac nhan lai", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("số điện thoại và địa chỉ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("so dien thoai va dia chi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chị gửi em", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chi gui em", response, StringComparison.OrdinalIgnoreCase);
        _draftOrderServiceMock.Verify(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateDraft_WhenExplicitBuyIntentIncludesContactInfo()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("selectedProductCodes", new List<string> { "KCN" });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

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
                Confidence = 0.95,
                Reason = "Customer wants to order and provided contact info"
            });

        _draftOrderServiceMock
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder
            {
                Id = Guid.NewGuid(),
                DraftCode = "DR-TEST-EXPLICIT",
                MerchandiseTotal = 320000m,
                ShippingFee = 30000m,
                GrandTotal = 350000m,
                Items = new List<DraftOrderItem>
                {
                    new() { ProductCode = "KCN", ProductName = "Kem Chong Nang", Quantity = 1, UnitPrice = 320000m }
                }
            });

        var response = await handler.HandleAsync(ctx, "Ok em len don luon, so cua chi la 0901234567, dia chi 12 Tran Hung Dao quan 1");

        Assert.Contains("tóm tắt đơn", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DR-TEST-EXPLICIT", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("320,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phí ship: em cần kiểm tra lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tổng đơn cuối: em sẽ báo lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("350,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xác nhận lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xac nhan lai", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("số điện thoại và địa chỉ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("so dien thoai va dia chi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chị gửi em", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chi gui em", response, StringComparison.OrdinalIgnoreCase);
        _draftOrderServiceMock.Verify(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldAskForContact_WhenBuyIntentArrivesAfterShippingCtaAndProductMustBeRecoveredFromHistory()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KL"))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KL"))
            .ReturnsAsync(new Gift { Code = "GIFT_KL", Name = "Tinh chat mini" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Dạ Kem Lua bên em hợp da khô ạ." }
        });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

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
                Intent = CustomerIntent.Confirming,
                Confidence = 0.95,
                Reason = "Customer accepted order after shipping answer"
            });

        var response = await handler.HandleAsync(ctx, "ok em");

        Assert.Contains("số điện thoại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("địa chỉ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("đã nhận được thông tin", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("da nhan duoc thong tin", response, StringComparison.OrdinalIgnoreCase);
        _draftOrderServiceMock.Verify(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldPreferLatestUserProduct_WhenHistoryHasConflictingProducts()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KCN"))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KL"))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("Kem Chong Nang", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m =>
                !m.Contains("Kem Chong Nang", StringComparison.OrdinalIgnoreCase) &&
                !m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KL"))
            .ReturnsAsync(new Gift { Code = "GIFT_KL", Name = "Tinh chat mini" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Cho chi kem chong nang" },
            new() { Role = "assistant", Content = "Dạ Kem Chong Nang bên em chống nắng tốt ạ." },
            new() { Role = "user", Content = "À thôi chị muốn xem Kem Lua" },
            new() { Role = "assistant", Content = "Dạ Kem Lua bên em cấp ẩm tốt ạ." }
        });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("KL");

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
                Intent = CustomerIntent.Confirming,
                Confidence = 0.95,
                Reason = "Customer accepted order after shipping answer"
            });

        var response = await handler.HandleAsync(ctx, "ok em");

        Assert.Contains("số điện thoại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("địa chỉ", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("KL", selectedCode);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.Is<string>(prompt => prompt.Contains("Chọn đúng 1 mã sản phẩm", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotCallAi_WhenLatestUserHistoryHasSingleCandidate()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KL"))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("Kem Lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KL"))
            .ReturnsAsync(new Gift { Code = "GIFT_KL", Name = "Tinh chat mini" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Dạ em hỗ trợ chị ngay đây ạ." },
            new() { Role = "user", Content = "Kem Lua nha em" },
            new() { Role = "assistant", Content = "Dạ em ghi nhận rồi ạ." }
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
                Intent = CustomerIntent.Confirming,
                Confidence = 0.95,
                Reason = "Customer accepted order after shipping answer"
            });

        var response = await handler.HandleAsync(ctx, "ok em");

        Assert.Contains("số điện thoại", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("KL", selectedCode);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.Is<string>(prompt => prompt.Contains("Chọn đúng 1 mã sản phẩm", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldKeepActiveProduct_WhenMessageOnlyAsksPolicy()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 350000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("dưỡng da", StringComparison.OrdinalIgnoreCase) || m.Contains("duong da", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("dưỡng da", StringComparison.OrdinalIgnoreCase) && !m.Contains("duong da", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks policy"
            });

        var response = await handler.HandleAsync(ctx, "có freeship không em, sản phẩm dưỡng da này thế nào");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotSwitchActiveProduct_WhenCustomerOnlyAsksToSeeAnotherProduct()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 350000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("kem lụa", StringComparison.OrdinalIgnoreCase) || m.Contains("kem lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("kem lụa", StringComparison.OrdinalIgnoreCase) && !m.Contains("kem lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer compares products"
            });

        var response = await handler.HandleAsync(ctx, "cho chị xem kem lụa với");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_ShouldKeepActiveProductInPolicyReply_WhenMessageMentionsOtherProductPromotion()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("kem trị nám", StringComparison.OrdinalIgnoreCase) || m.Contains("kem tri nam", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KTN", Name = "Kem Tri Nam", BasePrice = 520000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("kem trị nám", StringComparison.OrdinalIgnoreCase) && !m.Contains("kem tri nam", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Mat na mini phuc hoi" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks promotion policy"
            });

        var response = await handler.HandleAsync(ctx, "mặt nạ ngủ có khuyến mãi gì không em, chứ kem trị nám chị chưa cần");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mat na mini phuc hoi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kem Tri Nam", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseSafeShippingFallback_WhenCustomerAsksFreeship()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Serum duong da sample 5ml" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks shipping policy"
            });

        var response = await handler.HandleAsync(ctx, "mặt nạ ngủ có freeship không em");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chưa dám chốt freeship", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Serum duong da sample 5ml", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30,000đ", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseSafeInventoryFallback_WhenVariantIsNotSelected()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks inventory"
            });

        var response = await handler.HandleAsync(ctx, "mặt nạ ngủ còn hàng không em");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chưa xác nhận tồn kho chắc", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldSwitchActiveProduct_WhenCustomerExplicitlyChangesSelectedProduct()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KL"))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => m.Contains("kem lụa", StringComparison.OrdinalIgnoreCase) || m.Contains("kem lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(new Product { Code = "KL", Name = "Kem Lua", BasePrice = 410000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(m => !m.Contains("kem lụa", StringComparison.OrdinalIgnoreCase) && !m.Contains("kem lua", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Confidence = 0.95,
                Reason = "Customer explicitly switches product"
            });

        var response = await handler.HandleAsync(ctx, "chị đổi sang kem lụa nhé");

        Assert.Contains("Kem Lua", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("KL", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_ShouldSummarizeLockedProductInDraftConfirmation()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Mat na mini phuc hoi" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });
        ctx.SetData("selectedGiftCode", "GIFT_MN");
        ctx.SetData("selectedGiftName", "Mat na mini phuc hoi");
        ctx.SetData("selectedProductQuantities", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MN"] = 2
        });
        ctx.SetData("customerPhone", "0901234567");
        ctx.SetData("shippingAddress", "12 Tran Hung Dao");
        ctx.SetData("shippingFee", 30000m);
        ctx.SetData("contactNeedsConfirmation", false);

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
                Confidence = 0.95,
                Reason = "Customer is ready to order"
            });

        _draftOrderServiceMock
            .Setup(x => x.CreateFromContextAsync(It.IsAny<StateContext>(), default))
            .ReturnsAsync(new DraftOrder
            {
                Id = Guid.NewGuid(),
                DraftCode = "DR-TEST-MN",
                CustomerPhone = "0901234567",
                ShippingAddress = "12 Tran Hung Dao",
                MerchandiseTotal = 500000m,
                ShippingFee = 30000m,
                GrandTotal = 530000m,
                Items = new List<DraftOrderItem>
                {
                    new() { ProductCode = "MN", ProductName = "Mat Na Ngu", Quantity = 2, UnitPrice = 250000m }
                }
            });

        var response = await handler.HandleAsync(ctx, "ok em chốt đơn luôn");

        Assert.DoesNotContain("DR-TEST-MN", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mat Na Ngu x2", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phí ship: em cần kiểm tra lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tổng đơn cuối: em sẽ báo lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mat na mini phuc hoi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("530,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kem Tri Nam", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCatalogGroundingFallback_WhenCatalogQuestionHasNoGroundedProduct()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks for catalog list"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mặt nạ gạo, mặt nạ nhau thai cừu, mặt nạ vàng");

        var response = await handler.HandleAsync(ctx, "bên em có các loại mặt nạ nào?");

        Assert.Contains("chưa tìm thấy dữ liệu sản phẩm phù hợp", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mặt nạ gạo", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nhau thai cừu", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mặt nạ vàng", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCatalogGroundingFallback_WhenPriceQuestionHasNoGroundedProduct()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks product price"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mặt nạ gạo giá 150.000đ");

        var response = await handler.HandleAsync(ctx, "mặt nạ giá bao nhiêu?");

        Assert.Contains("chưa tìm thấy dữ liệu sản phẩm phù hợp", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("150.000", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mặt nạ gạo", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCatalogGroundingFallback_WhenRagHasNoValidatedProducts()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var ragService = new Mock<IRAGService>();
        ragService
            .Setup(x => x.RetrieveContextAsync("bên em có các loại mặt nạ nào?", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RAGContext(
                "Không tìm thấy sản phẩm phù hợp.",
                new List<string>(),
                new List<GroundedProduct>(),
                new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "empty")));

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            ragService.Object,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = true, TopK = 5 }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks for catalog list"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mặt nạ gạo, mặt nạ nhau thai cừu, mặt nạ vàng");

        var response = await handler.HandleAsync(ctx, "bên em có các loại mặt nạ nào?");

        Assert.Contains("chưa tìm thấy dữ liệu sản phẩm phù hợp", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFallback_WhenControlAiResponseContainsUngroundedProductFact()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", BasePrice = 320000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var responseValidationService = new Mock<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(
                It.Is<MessengerWebhook.Services.ResponseValidation.Models.ResponseValidationContext>(context =>
                    context.Response.Contains("Mặt nạ gạo", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessengerWebhook.Services.ResponseValidation.Models.ValidationResult
            {
                IsValid = false,
                Issues = new List<MessengerWebhook.Services.ResponseValidation.Models.ValidationIssue>
                {
                    new()
                    {
                        Category = "Grounding",
                        Severity = MessengerWebhook.Services.ResponseValidation.Models.ValidationSeverity.Error,
                        Message = "Ungrounded product fact"
                    }
                }
            });

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("control");

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });

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
                Confidence = 0.95,
                Reason = "Customer asks for skincare advice"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ chị có thể dùng Mặt nạ gạo giá 150.000đ ạ.");

        var response = await handler.HandleAsync(ctx, "loại này dùng thế nào em?");

        Assert.Contains("chưa tìm thấy dữ liệu", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mặt nạ gạo", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("150.000", response, StringComparison.OrdinalIgnoreCase);
        responseValidationService.Verify(x => x.ValidateAsync(
            It.IsAny<MessengerWebhook.Services.ResponseValidation.Models.ResponseValidationContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCatalogGroundingFallback_WhenSelectedProductCodeIsStale()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("OLD"))
            .ReturnsAsync((Product?)null);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "OLD" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks stale product price"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mặt nạ cũ giá 150.000đ");

        var response = await handler.HandleAsync(ctx, "mặt nạ giá bao nhiêu?");

        Assert.Contains("chưa tìm thấy dữ liệu sản phẩm phù hợp", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("150.000", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreatmentGroundingFailureWithRelatedSuggestions_ReturnsSuggestionWithoutGeminiRewrite()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("treatment");

        var responseValidationService = new Mock<IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(
                It.Is<ResponseValidationContext>(context =>
                    context.Response.Contains("Mặt nạ cấp ẩm Rau Má") &&
                    context.AllowedProductCodes.Contains("MN01") &&
                    context.RequiresFactGrounding),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        var groundingService = new Mock<IProductGroundingService>();
        groundingService
            .Setup(x => x.BuildContext(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>()))
            .Returns(new GroundedProductContext(true, Array.Empty<GroundedProduct>(), ProductGroundingService.FallbackReply));
        groundingService
            .Setup(x => x.BuildContextWithRelatedSuggestionsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedProductContext(
                true,
                Array.Empty<GroundedProduct>(),
                ProductGroundingService.FallbackReply,
                new[] { new GroundedProduct("p1", "MN01", "Mặt nạ cấp ẩm Rau Má", "Cosmetics", 120000m) },
                "Dạ hiện em chưa thấy đúng sản phẩm phù hợp trong catalog. Em có vài sản phẩm liên quan đang có dữ liệu trên hệ thống:\n1) Mặt nạ cấp ẩm Rau Má (MN01) - 120,000đ\nChị muốn xem sản phẩm nào ạ?"));
        groundingService
            .Setup(x => x.SanitizeAssistantHistory(It.IsAny<IEnumerable<AiConversationMessage>>(), It.IsAny<IReadOnlyCollection<GroundedProduct>>()))
            .Returns((IEnumerable<AiConversationMessage> history, IReadOnlyCollection<GroundedProduct> _) => history.ToList());

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>(),
            groundingService.Object);

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("selectedProductCodes", new List<string> { "OLD" });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Questioning, Confidence = 0.95, Reason = "Customer asks vague product" });

        var response = await handler.HandleAsync(ctx, "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm");

        Assert.Contains("Mặt nạ cấp ẩm Rau Má", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "OLD" }, ctx.GetData<List<string>>("selectedProductCodes"));
        _geminiService.Verify(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>(), It.IsAny<GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ControlGroundingFailureWithRelatedSuggestions_ReturnsSuggestion()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("control");

        var responseValidationService = new Mock<IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(It.IsAny<ResponseValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        var groundingService = new Mock<IProductGroundingService>();
        groundingService
            .Setup(x => x.BuildContext(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>()))
            .Returns(new GroundedProductContext(true, Array.Empty<GroundedProduct>(), ProductGroundingService.FallbackReply));
        groundingService
            .Setup(x => x.BuildContextWithRelatedSuggestionsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedProductContext(
                true,
                Array.Empty<GroundedProduct>(),
                ProductGroundingService.FallbackReply,
                new[] { new GroundedProduct("p2", "MN02", "Mặt nạ phục hồi B5", "Cosmetics", 150000m) },
                "Dạ hiện em chưa thấy đúng sản phẩm phù hợp trong catalog. Em có vài sản phẩm liên quan đang có dữ liệu trên hệ thống:\n1) Mặt nạ phục hồi B5 (MN02) - 150,000đ\nChị muốn xem sản phẩm nào ạ?"));
        groundingService
            .Setup(x => x.SanitizeAssistantHistory(It.IsAny<IEnumerable<AiConversationMessage>>(), It.IsAny<IReadOnlyCollection<GroundedProduct>>()))
            .Returns((IEnumerable<AiConversationMessage> history, IReadOnlyCollection<GroundedProduct> _) => history.ToList());

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>(),
            groundingService.Object);

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Questioning, Confidence = 0.95, Reason = "Customer asks vague product" });

        var response = await handler.HandleAsync(ctx, "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm");

        Assert.Contains("Mặt nạ phục hồi B5", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>(), It.IsAny<GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RagDisabledRealGroundingService_ReturnsRepositoryBackedSuggestion()
    {
        var tenantId = Guid.NewGuid();
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("control");

        var responseValidationService = new Mock<IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(
                It.Is<ResponseValidationContext>(context =>
                    context.Response.Contains("Mặt nạ cấp ẩm Rau Má") &&
                    context.AllowedProductCodes.Contains("MN01") &&
                    context.AllowedProductCodes.Count == 1 &&
                    context.RequiresFactGrounding),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(x => x.GetActiveRelatedAsync(
                tenantId,
                ProductCategory.Cosmetics,
                It.Is<IReadOnlyCollection<string>>(terms => terms.Contains("mat na") && terms.Contains("dưỡng ẩm")),
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new() { Id = "p1", Code = "MN01", Name = "Mặt nạ cấp ẩm Rau Má", Category = ProductCategory.Cosmetics, BasePrice = 120000m }
            });

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(x => x.TenantId).Returns(tenantId);
        var groundingService = new ProductGroundingService(new ProductNeedDetector(), new ProductMentionDetector(), productRepository.Object, tenantContext.Object);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>(),
            groundingService);

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("selectedProductCodes", new List<string> { "OLD" });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Questioning, Confidence = 0.95, Reason = "Customer asks vague product" });

        var response = await handler.HandleAsync(ctx, "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm");

        Assert.Contains("Mặt nạ cấp ẩm Rau Má", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MN01", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "OLD" }, ctx.GetData<List<string>>("selectedProductCodes"));
        _geminiService.Verify(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>(), It.IsAny<GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        productRepository.Verify(x => x.GetActiveRelatedAsync(tenantId, ProductCategory.Cosmetics, It.IsAny<IReadOnlyCollection<string>>(), 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CustomerSelectsRelatedSuggestionByNumber_ResolvesProductFromAssistantHistory()
    {
        var product = new Product { Id = "mask-global", Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", Category = ProductCategory.Cosmetics, BasePrice = 320000m, IsActive = true };
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.Contains("(MN)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(product);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => !message.Contains("(MN)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(product);

        var giftSelectionService = new Mock<IGiftSelectionService>();
        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync((Gift?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:\n1) Mặt Nạ Ngủ Dưỡng Ẩm (MN) - 320,000đ\nChị muốn xem sản phẩm nào ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.ReadyToBuy, Confidence = 0.95, Reason = "Customer selected suggested product" });

        var response = await handler.HandleAsync(ctx, "sản phẩm 1");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN", selectedCode);
        _geminiService.Verify(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>(), It.IsAny<GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CustomerSelectsSecondRelatedSuggestionByNumber_ResolvesSecondProduct()
    {
        var firstProduct = new Product { Id = "mask-1", Code = "MN1", Name = "Mặt Nạ Cấp Ẩm Rau Má", Category = ProductCategory.Cosmetics, BasePrice = 220000m, IsActive = true };
        var secondProduct = new Product { Id = "mask-2", Code = "MN2", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", Category = ProductCategory.Cosmetics, BasePrice = 320000m, IsActive = true };
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("1)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MN1)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(firstProduct);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("2)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MN2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(secondProduct);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.Contains("(MN1)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MN2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(firstProduct);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => !message.Contains("(MN1)", StringComparison.OrdinalIgnoreCase) && !message.Contains("(MN2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN2"))
            .ReturnsAsync(secondProduct);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:\n1) Mặt Nạ Cấp Ẩm Rau Má (MN1) - 220,000đ\n2) Mặt Nạ Ngủ Dưỡng Ẩm (MN2) - 320,000đ\nChị muốn xem sản phẩm nào ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Consulting, Confidence = 0.95, Reason = "Misclassified numeric selection" });

        var response = await handler.HandleAsync(ctx, "sản phẩm 2");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mặt Nạ Cấp Ẩm Rau Má", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN2", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_CustomerSelectsRelatedSuggestionByNumber_DoesNotFallbackToOlderSuggestionList()
    {
        var oldProduct = new Product { Id = "old-mask", Code = "OLD2", Name = "Mặt Nạ Cũ Số 2", Category = ProductCategory.Cosmetics, BasePrice = 190000m, IsActive = true };
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("2)", StringComparison.OrdinalIgnoreCase) && message.Contains("(OLD2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(oldProduct);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("2)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MISS2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("1)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Dạ em gợi ý vài lựa chọn:\n1) Mặt Nạ Cũ Số 1 (OLD1) - 150,000đ\n2) Mặt Nạ Cũ Số 2 (OLD2) - 190,000đ", Timestamp = DateTime.UtcNow.AddMinutes(-4) },
            new() { Role = "user", Content = "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:\n1) Mặt Nạ Mới Số 1 (MISS1) - 220,000đ\n2) Mặt Nạ Mới Số 2 (MISS2) - 320,000đ\nChị muốn xem sản phẩm nào ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Consulting, Confidence = 0.95, Reason = "Customer selected current numbered suggestion" });
        _geminiService
            .Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<AiConversationMessage>>(), It.IsAny<GeminiModelType?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em chưa xác định được sản phẩm trong lựa chọn hiện tại ạ.");

        var response = await handler.HandleAsync(ctx, "sản phẩm 2");

        Assert.DoesNotContain("Mặt Nạ Cũ Số 2", response, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ctx.GetData<List<string>>("selectedProductCodes"));
    }

    [Fact]
    public async Task HandleAsync_CustomerSelectsRelatedSuggestionByNumber_OverridesStaleProductContext()
    {
        var oldProduct = new Product { Id = "old-mask", Code = "OLD", Name = "Mặt Nạ Cũ", Category = ProductCategory.Cosmetics, BasePrice = 190000m, IsActive = true };
        var firstProduct = new Product { Id = "mask-1", Code = "MN1", Name = "Mặt Nạ Cấp Ẩm Rau Má", Category = ProductCategory.Cosmetics, BasePrice = 220000m, IsActive = true };
        var secondProduct = new Product { Id = "mask-2", Code = "MN2", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", Category = ProductCategory.Cosmetics, BasePrice = 320000m, IsActive = true };
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("1)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MN1)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(firstProduct);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.StartsWith("2)", StringComparison.OrdinalIgnoreCase) && message.Contains("(MN2)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(secondProduct);
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("OLD"))
            .ReturnsAsync(oldProduct);
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN2"))
            .ReturnsAsync(secondProduct);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("selectedProductCodes", new List<string> { "OLD" });
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:\n1) Mặt Nạ Cấp Ẩm Rau Má (MN1) - 220,000đ\n2) Mặt Nạ Ngủ Dưỡng Ẩm (MN2) - 320,000đ\nChị muốn xem sản phẩm nào ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Consulting, Confidence = 0.95, Reason = "Customer selected a new numbered suggestion" });

        var response = await handler.HandleAsync(ctx, "sản phẩm 2");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mặt Nạ Cũ", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN2", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_CustomerSelectsRelatedSuggestionByNumber_ResolvesProductEvenWhenIntentIsConsulting()
    {
        var product = new Product { Id = "mask-global", Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", Category = ProductCategory.Cosmetics, BasePrice = 320000m, IsActive = true };
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => message.Contains("(MN)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync(product);
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.Is<string>(message => !message.Contains("(MN)", StringComparison.OrdinalIgnoreCase))))
            .ReturnsAsync((Product?)null);
        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(product);

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "assistant", Content = "Dạ hiện em chưa thấy đúng mã/tên cụ thể trong catalog. Em gợi ý vài lựa chọn có dữ liệu trên hệ thống:\n1) Mặt Nạ Ngủ Dưỡng Ẩm (MN) - 320,000đ\nChị muốn xem sản phẩm nào ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Consulting, Confidence = 0.95, Reason = "Misclassified numeric selection" });

        var response = await handler.HandleAsync(ctx, "sản phẩm 1");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        var selectedCode = Assert.Single(selectedCodes!);
        Assert.Equal("MN", selectedCode);
    }

    [Fact]
    public async Task HandleAsync_RelatedSuggestionValidationFails_ReturnsSafeFallback()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("control");

        var responseValidationService = new Mock<IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(It.IsAny<ResponseValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Issues = new List<ValidationIssue> { new() { Category = "Grounding", Message = "unexpected product" } }
            });

        var groundingService = new Mock<IProductGroundingService>();
        groundingService
            .Setup(x => x.BuildContext(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>()))
            .Returns(new GroundedProductContext(true, Array.Empty<GroundedProduct>(), ProductGroundingService.FallbackReply));
        groundingService
            .Setup(x => x.BuildContextWithRelatedSuggestionsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Product>>(), It.IsAny<IEnumerable<GroundedProduct>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroundedProductContext(
                true,
                Array.Empty<GroundedProduct>(),
                ProductGroundingService.FallbackReply,
                new[] { new GroundedProduct("p1", "MN01", "Mặt nạ cấp ẩm Rau Má", "Cosmetics", 120000m) },
                "Dạ gợi ý Mặt nạ cấp ẩm Rau Má (MN01) - 120,000đ"));
        groundingService
            .Setup(x => x.SanitizeAssistantHistory(It.IsAny<IEnumerable<AiConversationMessage>>(), It.IsAny<IReadOnlyCollection<GroundedProduct>>()))
            .Returns((IEnumerable<AiConversationMessage> history, IReadOnlyCollection<GroundedProduct> _) => history.ToList());

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>(),
            groundingService.Object);

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };

        _geminiService
            .Setup(x => x.DetectIntentAsync(It.IsAny<string>(), It.IsAny<ConversationState>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<List<AiConversationMessage>>(), default))
            .ReturnsAsync(new IntentDetectionResult { Intent = CustomerIntent.Questioning, Confidence = 0.95, Reason = "Customer asks vague product" });

        var response = await handler.HandleAsync(ctx, "tôi đang tìm sản phẩm mặt nạ dưỡng ẩm");

        Assert.Equal(ProductGroundingService.FallbackReply, response);
    }

    [Fact]
    public async Task HandleAsync_ShouldAllowNonCatalogShopQuestion()
    {
        var productMappingService = new Mock<IProductMappingService>();
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var abTestService = new Mock<IABTestService>();
        abTestService
            .Setup(x => x.GetVariantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("control");

        var responseValidationService = new Mock<IResponseValidationService>();
        responseValidationService
            .Setup(x => x.ValidateAsync(It.IsAny<ResponseValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            responseValidationService.Object,
            abTestService.Object,
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting, SessionId = "session-1" };

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks operating hours"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ shop có mở cửa hôm nay ạ.");

        var response = await handler.HandleAsync(ctx, "shop có mở cửa không?");

        Assert.DoesNotContain("chưa tìm thấy dữ liệu sản phẩm phù hợp", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mở cửa", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnBrowsingOffer_WithoutMissingInfoPrompt()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(new Product { Code = "KCN", Name = "Kem Chong Nang", BasePrice = 320000m });

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KCN"))
            .ReturnsAsync(new Gift { Code = "GIFT_KCN", Name = "Mat na duong sang" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>());

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
                Intent = CustomerIntent.Browsing,
                Confidence = 0.95,
                Reason = "Customer is browsing"
            });

        var response = await handler.HandleAsync(ctx, "bên em có kem chống nắng không");

        Assert.Contains("Kem Chong Nang", response);
        Assert.Contains("320,000đ", response);
        Assert.Contains("Mat na duong sang", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("so dien thoai", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseSafeOrderEstimateFallback_WhenCustomerAsksTotal()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Mat na mini phuc hoi" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });
        ctx.SetData("selectedProductQuantities", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["MN"] = 2
        });
        ctx.SetData("shippingFee", 30000m);

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks total order estimate"
            });

        var response = await handler.HandleAsync(ctx, "nếu lấy 2 hũ thì tổng tiền bao nhiêu em");

        Assert.Contains("Mat Na Ngu", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phí ship và tổng đơn cuối em cần kiểm tra lại", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("530,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30,000đ", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotConfirmPrice_WhenProductHasVariantsButNoVariantSelected()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("KTN"))
            .ReturnsAsync(new Product
            {
                Code = "KTN",
                Name = "Kem Tri Nam",
                BasePrice = 520000m,
                Variants = new List<ProductVariant>
                {
                    new() { Id = "v1", VolumeML = 30, Texture = "cream", Price = 520000m, StockQuantity = 5, IsAvailable = true }
                }
            });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("KTN"))
            .ReturnsAsync(new Gift { Code = "GIFT_KTN", Name = "Sample serum" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "KTN" });

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
                Intent = CustomerIntent.Questioning,
                Confidence = 0.95,
                Reason = "Customer asks product price"
            });

        var response = await handler.HandleAsync(ctx, "kem trị nám giá bao nhiêu em");

        Assert.Contains("chưa dám chốt giá chính xác", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("520,000đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(false, ctx.GetData<bool?>("price_confirmed"));
    }

    [Fact]
    public async Task HandleAsync_ShouldRefreshStalePolicyContextBeforeOffer()
    {
        var productMappingService = new Mock<IProductMappingService>();
        var giftSelectionService = new Mock<IGiftSelectionService>();

        productMappingService
            .Setup(x => x.GetActiveProductByCodeAsync("MN"))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });
        productMappingService
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(new Product { Code = "MN", Name = "Mat Na Ngu", BasePrice = 250000m });

        giftSelectionService
            .Setup(x => x.SelectGiftForProductAsync("MN"))
            .ReturnsAsync(new Gift { Code = "GIFT_MN", Name = "Mat na mini phuc hoi" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            new PolicyGuardService(Options.Create(_salesBotOptions)),
            productMappingService.Object,
            giftSelectionService.Object,
            new FreeshipCalculator(),
            Mock.Of<ICaseEscalationService>(),
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };
        ctx.SetData("selectedProductCodes", new List<string> { "MN" });
        ctx.SetData("selectedGiftCode", "GIFT_OLD");
        ctx.SetData("selectedGiftName", "Qua cu sai");
        ctx.SetData("shippingFee", 0m);

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
                Intent = CustomerIntent.Browsing,
                Confidence = 0.95,
                Reason = "Customer is browsing"
            });

        var response = await handler.HandleAsync(ctx, "mặt nạ ngủ giá sao em");

        Assert.Contains("250,000đ", response);
        Assert.Contains("Mat na mini phuc hoi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Qua cu sai", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mat na mini phuc hoi", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Qua cu sai", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldStillRunIntentAi_WhenPolicySemanticAlreadyAttemptedButActionAllows()
    {
        var policyGuardService = new Mock<IPolicyGuardService>();
        var salesBotOptions = new SalesBotOptions
        {
            MaxConsultationAttempts = 2,
            ConversationHistoryLimit = 15,
            IntentConfidenceThreshold = 0.7
        };
        var policyGuardOptions = new PolicyGuardOptions
        {
            SafeReplyMessage = "Safe reply from policy guard."
        };
        var caseEscalationService = new Mock<ICaseEscalationService>(MockBehavior.Strict);

        policyGuardService
            .Setup(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyDecision(
                false,
                SupportCaseReason.ManualReview,
                "semantic allow",
                PolicyAction.Allow,
                0.2m,
                0.4m,
                [],
                true));

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            policyGuardService.Object,
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            caseEscalationService.Object,
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

        _geminiService
            .Setup(x => x.DetectIntentAsync(
                It.IsAny<string>(),
                It.IsAny<ConversationState>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentDetectionResult
            {
                Intent = CustomerIntent.Consulting,
                Confidence = 0.9,
                Reason = "Asked a follow-up question"
            });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        var response = await handler.HandleAsync(ctx, "Cho chị hỏi thêm về sản phẩm");

        Assert.NotEqual(salesBotOptions.UnsupportedFallbackMessage, response);
        Assert.NotEqual(ConversationState.HumanHandoff, ctx.CurrentState);
        policyGuardService.Verify(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiService.Verify(x => x.DetectIntentAsync(
            It.IsAny<string>(),
            It.IsAny<ConversationState>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSafeReply_WhenPolicyReturnsSafeReply()
    {
        var policyGuardService = new Mock<IPolicyGuardService>();
        var salesBotOptions = new SalesBotOptions
        {
            MaxConsultationAttempts = 2,
            ConversationHistoryLimit = 15,
            IntentConfidenceThreshold = 0.7
        };
        var policyGuardOptions = new PolicyGuardOptions
        {
            SafeReplyMessage = "Safe reply from policy guard."
        };
        var caseEscalationService = new Mock<ICaseEscalationService>(MockBehavior.Strict);

        policyGuardService
            .Setup(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PolicyDecision(
                false,
                SupportCaseReason.UnsupportedQuestion,
                "safe reply",
                PolicyAction.SafeReply,
                0.5m,
                0.8m,
                [],
                true));

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            policyGuardService.Object,
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            caseEscalationService.Object,
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Mock.Of<ISubIntentClassifier>(),
            Options.Create(salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Consulting };

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Dạ em tư vấn cho chị ạ");

        var response = await handler.HandleAsync(ctx, "Ship thế nào em?");

        Assert.Equal(policyGuardOptions.SafeReplyMessage, response);
        Assert.NotEqual(ConversationState.HumanHandoff, ctx.CurrentState);
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory");
        Assert.NotNull(history);
        Assert.Equal(policyGuardOptions.SafeReplyMessage, history!.Last().Content);
        policyGuardService.Verify(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiService.Verify(x => x.DetectIntentAsync(
            It.IsAny<string>(),
            It.IsAny<ConversationState>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        caseEscalationService.Verify(x => x.EscalateAsync(
            It.IsAny<string>(),
            It.IsAny<SupportCaseReason>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldPassPolicyRequestContext_AndEscalateThroughExistingFlow()
    {
        var policyGuardService = new Mock<IPolicyGuardService>();
        var caseEscalationService = new Mock<ICaseEscalationService>();
        var capturedRequests = new List<PolicyGuardRequest>();
        var selectedProductCodes = new List<string> { "MN", "KL" };
        var supportCaseId = Guid.NewGuid();
        var existingSupportCaseId = Guid.NewGuid();
        var draftOrderId = Guid.NewGuid();

        policyGuardService
            .Setup(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PolicyGuardRequest, CancellationToken>((request, _) => capturedRequests.Add(request))
            .ReturnsAsync(new PolicyDecision(
                true,
                SupportCaseReason.RefundRequest,
                "keyword: hoan tien",
                PolicyAction.HardEscalate,
                0.95m,
                1m,
                []));

        caseEscalationService
            .Setup(x => x.EscalateAsync(
                "test-psid",
                SupportCaseReason.RefundRequest,
                "keyword: hoan tien",
                "Hoàn tiền giúp chị nhé",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HumanSupportCase { Id = supportCaseId, FacebookPSID = "test-psid" });

        var handler = new TestSalesStateHandler(
            _geminiService.Object,
            policyGuardService.Object,
            Mock.Of<IProductMappingService>(),
            Mock.Of<IGiftSelectionService>(),
            new FreeshipCalculator(),
            caseEscalationService.Object,
            _customerService.Object,
            new DraftOrderCoordinator(_draftOrderServiceMock.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<DraftOrderCoordinator>.Instance),
            null,
            Mock.Of<MessengerWebhook.Services.Emotion.IEmotionDetectionService>(),
            Mock.Of<MessengerWebhook.Services.Tone.IToneMatchingService>(),
            Mock.Of<MessengerWebhook.Services.Conversation.IConversationContextAnalyzer>(),
            Mock.Of<MessengerWebhook.Services.SmallTalk.ISmallTalkService>(),
            Mock.Of<MessengerWebhook.Services.ResponseValidation.IResponseValidationService>(),
            Mock.Of<IABTestService>(),
            Mock.Of<IConversationMetricsService>(),
            Options.Create(_salesBotOptions),
            Options.Create(new RAGOptions { Enabled = false }),
            Mock.Of<ILogger<TestSalesStateHandler>>());

        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CollectingInfo };
        ctx.SetData("facebookPageId", "page-01");
        ctx.SetData("selectedProductCodes", selectedProductCodes);
        ctx.SetData("draftOrderId", draftOrderId);
        ctx.SetData("draftOrderCode", "DR-001");
        ctx.SetData("supportCaseId", existingSupportCaseId);
        ctx.SetData("knownIntent", "ready-to-buy");
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Chị cần em hỗ trợ gì thêm ạ?", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new() { Role = "user", Content = "Chị muốn xem lại đơn trước đó", Timestamp = DateTime.UtcNow.AddMinutes(-1) }
        });

        var response = await handler.HandleAsync(ctx, "Hoàn tiền giúp chị nhé");

        var request = Assert.Single(capturedRequests);
        Assert.Equal("Hoàn tiền giúp chị nhé", request.Message);
        Assert.Equal("test-psid", request.FacebookPSID);
        Assert.Equal("page-01", request.FacebookPageId);
        Assert.Equal(ConversationState.CollectingInfo.ToString(), request.CurrentState);
        Assert.Equal("ready-to-buy", request.KnownIntent);
        Assert.True(request.HasDraftOrder);
        Assert.True(request.HasOpenSupportCase);
        Assert.Equal(new[] { "MN", "KL" }, request.SelectedProductCodes);
        Assert.NotSame(selectedProductCodes, request.SelectedProductCodes);
        selectedProductCodes.Add("NEW");
        Assert.Equal(new[] { "MN", "KL" }, request.SelectedProductCodes);
        Assert.Equal(3, request.RecentTurns!.Count);
        Assert.Equal("user", request.RecentTurns[^1].Role);
        Assert.Equal("Hoàn tiền giúp chị nhé", request.RecentTurns[^1].Content);

        Assert.Equal(_salesBotOptions.UnsupportedFallbackMessage, response);
        Assert.Equal(ConversationState.HumanHandoff, ctx.CurrentState);
        Assert.Equal(supportCaseId, ctx.GetData<Guid?>("supportCaseId"));

        caseEscalationService.VerifyAll();
        policyGuardService.Verify(x => x.EvaluateAsync(It.IsAny<PolicyGuardRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _geminiService.Verify(x => x.DetectIntentAsync(
            It.IsAny<string>(),
            It.IsAny<ConversationState>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<List<AiConversationMessage>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test handler implementation
    public class TestSalesStateHandler : SalesStateHandlerBase
    {
        public override ConversationState HandledState => ConversationState.Consulting;

        public TestSalesStateHandler(
            IGeminiService geminiService,
            IPolicyGuardService policyGuardService,
            IProductMappingService productMappingService,
            IGiftSelectionService giftSelectionService,
            IFreeshipCalculator freeshipCalculator,
            ICaseEscalationService caseEscalationService,
            ICustomerIntelligenceService customerIntelligenceService,
            DraftOrderCoordinator draftOrderCoordinator,
            MessengerWebhook.Services.RAG.IRAGService? ragService,
            MessengerWebhook.Services.Emotion.IEmotionDetectionService emotionDetectionService,
            MessengerWebhook.Services.Tone.IToneMatchingService toneMatchingService,
            MessengerWebhook.Services.Conversation.IConversationContextAnalyzer conversationContextAnalyzer,
            MessengerWebhook.Services.SmallTalk.ISmallTalkService smallTalkService,
            MessengerWebhook.Services.ResponseValidation.IResponseValidationService responseValidationService,
            IABTestService abTestService,
            IConversationMetricsService conversationMetricsService,
            ISubIntentClassifier subIntentClassifier,
            IOptions<SalesBotOptions> salesBotOptions,
            IOptions<RAGOptions> ragOptions,
            ILogger<TestSalesStateHandler> logger,
            IProductGroundingService? productGroundingService = null)
            : base(
                geminiService,
                policyGuardService,
                productMappingService,
                giftSelectionService,
                freeshipCalculator,
                caseEscalationService,
                customerIntelligenceService,
                draftOrderCoordinator,
                ragService,
                emotionDetectionService,
                toneMatchingService,
                conversationContextAnalyzer,
                smallTalkService,
                responseValidationService,
                abTestService,
                conversationMetricsService,
                Mock.Of<ISubIntentClassifier>(),
                salesBotOptions,
                ragOptions,
                logger,
                productGroundingService)
        {
        }

        public TestSalesStateHandler(
            IGeminiService geminiService,
            IPolicyGuardService policyGuardService,
            IProductMappingService productMappingService,
            IGiftSelectionService giftSelectionService,
            IFreeshipCalculator freeshipCalculator,
            ICaseEscalationService caseEscalationService,
            ICustomerIntelligenceService customerIntelligenceService,
            DraftOrderCoordinator draftOrderCoordinator,
            MessengerWebhook.Services.RAG.IRAGService? ragService,
            MessengerWebhook.Services.Emotion.IEmotionDetectionService emotionDetectionService,
            MessengerWebhook.Services.Tone.IToneMatchingService toneMatchingService,
            MessengerWebhook.Services.Conversation.IConversationContextAnalyzer conversationContextAnalyzer,
            MessengerWebhook.Services.SmallTalk.ISmallTalkService smallTalkService,
            MessengerWebhook.Services.ResponseValidation.IResponseValidationService responseValidationService,
            IABTestService abTestService,
            IConversationMetricsService conversationMetricsService,
            IOptions<SalesBotOptions> salesBotOptions,
            IOptions<RAGOptions> ragOptions,
            ILogger<TestSalesStateHandler> logger,
            IProductGroundingService? productGroundingService = null)
            : this(
                geminiService,
                policyGuardService,
                productMappingService,
                giftSelectionService,
                freeshipCalculator,
                caseEscalationService,
                customerIntelligenceService,
                draftOrderCoordinator,
                ragService,
                emotionDetectionService,
                toneMatchingService,
                conversationContextAnalyzer,
                smallTalkService,
                responseValidationService,
                abTestService,
                conversationMetricsService,
                Mock.Of<ISubIntentClassifier>(),
                salesBotOptions,
                ragOptions,
                logger,
                productGroundingService)
        {
        }

        protected override Task<string> HandleInternalAsync(StateContext ctx, string message)
        {
            // Call base HandleSalesConversationAsync to test the tracking logic
            return HandleSalesConversationAsync(ctx, message);
        }
    }
}
