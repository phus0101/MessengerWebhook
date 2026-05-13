using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.UnitTests.Services.Sales.Context;

public class SalesContextResolverTests
{
    private readonly Mock<ICustomerIntelligenceService> _mockCustomerIntelligence;
    private readonly Mock<IProductMappingService> _mockProductMapping;
    private readonly Mock<IGiftSelectionService> _mockGiftSelection;
    private readonly Mock<IFreeshipCalculator> _mockFreeshipCalculator;
    private readonly Mock<IProductGroundingService> _mockProductGrounding;
    private readonly Mock<IGeminiService> _mockGeminiService;
    private readonly ILogger<SalesContextResolver> _logger;
    private readonly SalesContextResolver _resolver;

    public SalesContextResolverTests()
    {
        _mockCustomerIntelligence = new Mock<ICustomerIntelligenceService>();
        _mockProductMapping = new Mock<IProductMappingService>();
        _mockGiftSelection = new Mock<IGiftSelectionService>();
        _mockFreeshipCalculator = new Mock<IFreeshipCalculator>();
        _mockProductGrounding = new Mock<IProductGroundingService>();
        _mockGeminiService = new Mock<IGeminiService>();
        _logger = NullLogger<SalesContextResolver>.Instance;

        _resolver = new SalesContextResolver(
            _mockCustomerIntelligence.Object,
            _mockProductMapping.Object,
            _mockGiftSelection.Object,
            _mockFreeshipCalculator.Object,
            _mockProductGrounding.Object,
            _mockGeminiService.Object,
            _logger);
    }

    #region GetVipProfileAsync Tests

    [Fact]
    public async Task GetVipProfileAsync_ShouldReturnVipProfile_WhenCustomerExists()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("facebookPageId", "page-123");
        var customer = new CustomerIdentity { Id = Guid.NewGuid() };
        var vipProfile = new VipProfile { IsVip = true, TotalOrders = 5 };

        _mockCustomerIntelligence
            .Setup(x => x.GetExistingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _mockCustomerIntelligence
            .Setup(x => x.GetVipProfileAsync(customer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vipProfile);

        // Act
        var result = await _resolver.GetVipProfileAsync(ctx);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsVip);
        Assert.Equal(5, result.TotalOrders);
    }

    [Fact]
    public async Task GetVipProfileAsync_ShouldReturnNull_WhenCustomerNotFound()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("facebookPageId", "page-123");

        _mockCustomerIntelligence
            .Setup(x => x.GetExistingAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerIdentity?)null);
        _mockCustomerIntelligence
            .Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerIdentity?)null);

        // Act
        var result = await _resolver.GetVipProfileAsync(ctx);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetActiveSelectedProductsAsync Tests

    [Fact]
    public async Task GetActiveSelectedProductsAsync_ShouldReturnProducts_WhenCodesFound()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1", "PROD2" });

        var product1 = new Product { Code = "PROD1", Name = "Product 1" };
        var product2 = new Product { Code = "PROD2", Name = "Product 2" };

        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD1"))
            .ReturnsAsync(product1);
        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD2"))
            .ReturnsAsync(product2);

        // Act
        var result = await _resolver.GetActiveSelectedProductsAsync(ctx);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(product1, result);
        Assert.Contains(product2, result);
    }

    [Fact]
    public async Task GetActiveSelectedProductsAsync_ShouldFilterNullProducts_WhenSomeNotFound()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1", "PROD_MISSING", "PROD2" });

        var product1 = new Product { Code = "PROD1", Name = "Product 1" };
        var product2 = new Product { Code = "PROD2", Name = "Product 2" };

        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD1"))
            .ReturnsAsync(product1);
        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD_MISSING"))
            .ReturnsAsync((Product?)null);
        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD2"))
            .ReturnsAsync(product2);

        // Act
        var result = await _resolver.GetActiveSelectedProductsAsync(ctx);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain((Product?)null, result);
    }

    [Fact]
    public async Task GetActiveSelectedProductsAsync_ShouldReturnEmpty_WhenNoCodes()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string>());

        // Act
        var result = await _resolver.GetActiveSelectedProductsAsync(ctx);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveSelectedProductsAsync_ShouldDeduplicateCodes()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1", "PROD1" });

        var product = new Product { Code = "PROD1", Name = "Product 1" };
        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD1"))
            .ReturnsAsync(product);

        // Act
        var result = await _resolver.GetActiveSelectedProductsAsync(ctx);

        // Assert
        Assert.Single(result);
    }

    #endregion

    #region GetActiveProductOrResolveAsync Tests

    [Fact]
    public async Task GetActiveProductOrResolveAsync_ShouldReturnActiveProduct_WhenExists()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });

        var activeProduct = new Product { Code = "PROD1", Name = "Product 1" };
        _mockProductMapping
            .Setup(x => x.GetActiveProductByCodeAsync("PROD1"))
            .ReturnsAsync(activeProduct);

        // Act
        var result = await _resolver.GetActiveProductOrResolveAsync(ctx, "tell me about PROD1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PROD1", result.Code);
    }

    [Fact]
    public async Task GetActiveProductOrResolveAsync_ShouldFallThrough_WhenNoActiveSelected()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string>());

        var matchedProduct = new Product { Code = "PROD2", Name = "Product 2" };
        _mockProductMapping
            .Setup(x => x.GetProductByMessageAsync("tell me about PROD2"))
            .ReturnsAsync(matchedProduct);

        // Act
        var result = await _resolver.GetActiveProductOrResolveAsync(ctx, "tell me about PROD2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PROD2", result.Code);
    }

    #endregion

    #region ApplyResolvedProductAsync Tests

    [Fact]
    public async Task ApplyResolvedProductAsync_ShouldSetProductAndGift_WhenGiftFound()
    {
        // Arrange
        var ctx = new StateContext();
        var product = new Product { Code = "PROD1", Name = "Product 1" };
        var gift = new Gift { Code = "GIFT1", Name = "Free Gift" };

        _mockGiftSelection
            .Setup(x => x.SelectGiftForProductAsync("PROD1"))
            .ReturnsAsync(gift);
        _mockFreeshipCalculator
            .Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>()))
            .Returns(50000m);

        // Act
        await _resolver.ApplyResolvedProductAsync(ctx, product, "test-source");

        // Assert
        var selectedCodes = ctx.GetData<List<string>>("selectedProductCodes");
        Assert.Contains("PROD1", selectedCodes);
        Assert.Equal("GIFT1", ctx.GetData<string>("selectedGiftCode"));
        Assert.Equal("Free Gift", ctx.GetData<string>("selectedGiftName"));
        Assert.Equal(50000m, ctx.GetData<decimal>("shippingFee"));
    }

    [Fact]
    public async Task ApplyResolvedProductAsync_ShouldHandleNoGift_WhenNotFound()
    {
        // Arrange
        var ctx = new StateContext();
        var product = new Product { Code = "PROD1", Name = "Product 1" };

        _mockGiftSelection
            .Setup(x => x.SelectGiftForProductAsync("PROD1"))
            .ReturnsAsync((Gift?)null);
        _mockFreeshipCalculator
            .Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>()))
            .Returns(50000m);

        // Act
        await _resolver.ApplyResolvedProductAsync(ctx, product, "test-source");

        // Assert
        Assert.Equal("", ctx.GetData<string>("selectedGiftCode"));
        Assert.Equal("", ctx.GetData<string>("selectedGiftName"));
    }

    [Fact]
    public async Task ApplyResolvedProductAsync_ShouldSetSourceTracking()
    {
        // Arrange
        var ctx = new StateContext();
        var product = new Product { Code = "PROD1" };

        _mockGiftSelection.Setup(x => x.SelectGiftForProductAsync(It.IsAny<string>())).ReturnsAsync((Gift?)null);
        _mockFreeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(0m);

        // Act
        await _resolver.ApplyResolvedProductAsync(ctx, product, "history-recovery");

        // Assert
        Assert.Equal("PROD1", ctx.GetData<string>("lastResolvedProductCode"));
        Assert.Equal("history-recovery", ctx.GetData<string>("lastResolvedProductSource"));
    }

    #endregion

    #region SyncActiveProductPolicyContextAsync Tests

    [Fact]
    public async Task SyncActiveProductPolicyContextAsync_ShouldUpdateGiftAndShipping_WhenGiftFound()
    {
        // Arrange
        var ctx = new StateContext();
        var gift = new Gift { Code = "GIFT1", Name = "Free Gift" };

        _mockGiftSelection
            .Setup(x => x.SelectGiftForProductAsync("PROD1"))
            .ReturnsAsync(gift);
        _mockFreeshipCalculator
            .Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>()))
            .Returns(50000m);

        // Act
        await _resolver.SyncActiveProductPolicyContextAsync(ctx, "PROD1");

        // Assert
        Assert.Equal("GIFT1", ctx.GetData<string>("selectedGiftCode"));
        Assert.Equal("Free Gift", ctx.GetData<string>("selectedGiftName"));
        Assert.Equal(50000m, ctx.GetData<decimal>("shippingFee"));
    }

    #endregion

    #region BuildCommercialFactSnapshotAsync Tests

    [Fact]
    public async Task BuildCommercialFactSnapshotAsync_ShouldCreateSnapshot_WithVariant()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedVariantId", "VAR1");
        ctx.SetData("shippingFee", 50000m);

        var variant = new ProductVariant { Id = "VAR1", Price = 100000m, IsAvailable = true, StockQuantity = 10 };
        var product = new Product
        {
            Code = "PROD1",
            BasePrice = 100000m,
            Variants = new List<ProductVariant> { variant }
        };
        var gift = new Gift { Name = "Free Gift" };

        _mockGiftSelection
            .Setup(x => x.SelectGiftForProductAsync("PROD1"))
            .ReturnsAsync(gift);

        // Act
        var result = await _resolver.BuildCommercialFactSnapshotAsync(ctx, product);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.GiftConfirmed);
        Assert.Equal("Free Gift", result.GiftName);
    }

    [Fact]
    public async Task BuildCommercialFactSnapshotAsync_ShouldHandleNoVariant_WhenNotSelected()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedVariantId", "");
        ctx.SetData("shippingFee", null);

        var product = new Product
        {
            Code = "PROD1",
            BasePrice = 100000m,
            Variants = new List<ProductVariant>()
        };

        _mockGiftSelection
            .Setup(x => x.SelectGiftForProductAsync("PROD1"))
            .ReturnsAsync((Gift?)null);

        // Act
        var result = await _resolver.BuildCommercialFactSnapshotAsync(ctx, product);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.InventoryConfirmed);
    }

    #endregion

    #region IsRelatedSuggestionSelection Tests

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("20")]
    public void IsRelatedSuggestionSelection_ShouldReturnTrue_ForValidNumbers(string message)
    {
        // Act
        var result = _resolver.IsRelatedSuggestionSelection(message);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("chon so 1")]
    [InlineData("san pham so 3")]
    [InlineData("lua chon 2")]
    public void IsRelatedSuggestionSelection_ShouldReturnTrue_ForNumberedPatterns(string message)
    {
        // Act
        var result = _resolver.IsRelatedSuggestionSelection(message);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("random text")]
    [InlineData("21")]
    public void IsRelatedSuggestionSelection_ShouldReturnFalse_ForInvalidMessages(string? message)
    {
        // Act
        var result = _resolver.IsRelatedSuggestionSelection(message ?? "");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ExtractRelatedSuggestionSelectionNumber Tests

    [Theory]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    [InlineData("20", 20)]
    public void ExtractRelatedSuggestionSelectionNumber_ShouldExtract_FromPureNumber(string message, int expected)
    {
        // Act
        var result = _resolver.ExtractRelatedSuggestionSelectionNumber(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("chon so 1", 1)]
    [InlineData("san pham so 3", 3)]
    [InlineData("lua chon 2", 2)]
    [InlineData("chon mon 5", 5)]
    public void ExtractRelatedSuggestionSelectionNumber_ShouldExtract_FromPattern(string message, int expected)
    {
        // Act
        var result = _resolver.ExtractRelatedSuggestionSelectionNumber(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("random text")]
    [InlineData("0")]
    [InlineData("21")]
    public void ExtractRelatedSuggestionSelectionNumber_ShouldReturnNull_ForInvalidMessages(string? message)
    {
        // Act
        var result = _resolver.ExtractRelatedSuggestionSelectionNumber(message);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CollectHistoryProductCandidatesAsync Tests

    [Fact]
    public async Task CollectHistoryProductCandidatesAsync_ShouldCollectProducts_FromMatchedMessages()
    {
        // Arrange
        var product1 = new Product { Code = "PROD1", Name = "Product 1" };
        var product2 = new Product { Code = "PROD2", Name = "Product 2" };

        var messages = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Interested in PROD1" },
            new() { Role = "assistant", Content = "PROD1 is great" },
            new() { Role = "user", Content = "What about PROD2?" }
        };

        _mockProductMapping
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .Returns((string msg) =>
            {
                if (msg.Contains("PROD1", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<Product?>(product1);
                if (msg.Contains("PROD2", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<Product?>(product2);
                return Task.FromResult<Product?>(null);
            });

        // Act
        var result = await _resolver.CollectHistoryProductCandidatesAsync(messages, "user");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, c => Assert.NotNull(c.Product));
    }

    [Fact]
    public async Task CollectHistoryProductCandidatesAsync_ShouldDeduplicate_SameCodes()
    {
        // Arrange
        var product = new Product { Code = "PROD1", Name = "Product 1" };

        var messages = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "I like PROD1" },
            new() { Role = "user", Content = "PROD1 is the best" }
        };

        _mockProductMapping
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(product);

        // Act
        var result = await _resolver.CollectHistoryProductCandidatesAsync(messages, "user");

        // Assert
        Assert.Single(result);
    }

    #endregion

    #region ResolveAmbiguousHistoryProductCandidateAsync Tests

    [Fact]
    public async Task ResolveAmbiguousHistoryProductCandidateAsync_ShouldUseAI_WhenMultipleCandidates()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };

        var product1 = new Product { Code = "PROD1", Name = "Product 1", Category = ProductCategory.Cosmetics };
        var product2 = new Product { Code = "PROD2", Name = "Product 2", Category = ProductCategory.Fashion };

        var candidates = new List<HistoryProductCandidate>
        {
            new(product1, "user", "I want PROD1"),
            new(product2, "assistant", "PROD2 is available")
        };

        var messages = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "I want PROD1" },
            new() { Role = "assistant", Content = "PROD2 is available" }
        };

        _mockProductGrounding
            .Setup(x => x.SanitizeAssistantHistory(It.IsAny<List<AiConversationMessage>>(), It.IsAny<List<GroundedProduct>>()))
            .Returns(messages);

        _mockGeminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<AiConversationMessage>>(),
                It.IsAny<GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("PROD1");

        // Act
        var result = await _resolver.ResolveAmbiguousHistoryProductCandidateAsync(ctx, messages, candidates, "user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PROD1", result.Product.Code);
    }

    #endregion

    #region TryExtractProductFromHistoryAsync Tests

    [Fact]
    public async Task TryExtractProductFromHistoryAsync_ShouldSkip_WhenActiveProductExists()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });
        ctx.SetData("conversationHistory", new List<AiConversationMessage>());

        // Act
        await _resolver.TryExtractProductFromHistoryAsync(ctx);

        // Assert
        var codes = ctx.GetData<List<string>>("selectedProductCodes");
        Assert.Contains("PROD1", codes);
        _mockProductMapping.Verify(x => x.GetProductByMessageAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryExtractProductFromHistoryAsync_ShouldExtract_WhenNoActiveProduct()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("selectedProductCodes", new List<string>());

        var product = new Product { Code = "PROD1", Name = "Product 1" };
        var messages = new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "I want PROD1" }
        };
        ctx.SetData("conversationHistory", messages);

        _mockProductMapping
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(product);
        _mockGiftSelection.Setup(x => x.SelectGiftForProductAsync(It.IsAny<string>())).ReturnsAsync((Gift?)null);
        _mockFreeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(0m);

        // Act
        await _resolver.TryExtractProductFromHistoryAsync(ctx);

        // Assert
        var codes = ctx.GetData<List<string>>("selectedProductCodes");
        Assert.NotNull(codes);
        Assert.NotEmpty(codes);
    }

    #endregion

    #region TryResolveNumberedSuggestionSelectionAsync Tests

    [Fact]
    public async Task TryResolveNumberedSuggestionSelectionAsync_ShouldResolve_WhenValidNumber()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        var product = new Product { Code = "PROD1", Name = "Product 1" };

        var messages = new List<AiConversationMessage>
        {
            new()
            {
                Role = "assistant",
                Content = "1) Product 1\n2) Product 2\n3) Product 3"
            }
        };

        ctx.SetData("conversationHistory", messages);

        _mockProductMapping
            .Setup(x => x.GetProductByMessageAsync(It.IsAny<string>()))
            .ReturnsAsync(product);
        _mockGiftSelection.Setup(x => x.SelectGiftForProductAsync(It.IsAny<string>())).ReturnsAsync((Gift?)null);
        _mockFreeshipCalculator.Setup(x => x.CalculateShippingFee(It.IsAny<List<string>>())).Returns(0m);

        // Act
        var result = await _resolver.TryResolveNumberedSuggestionSelectionAsync(ctx, "1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PROD1", result.Code);
    }

    [Fact]
    public async Task TryResolveNumberedSuggestionSelectionAsync_ShouldReturnNull_WhenNoValidNumber()
    {
        // Arrange
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("conversationHistory", new List<AiConversationMessage>());

        // Act
        var result = await _resolver.TryResolveNumberedSuggestionSelectionAsync(ctx, "random text");

        // Assert
        Assert.Null(result);
    }

    #endregion
}
