using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Models;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.StateMachine.Handlers;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.StateMachine.Handlers;

public class LegacyFalseFactStateHandlerTests
{
    private readonly Mock<IGeminiService> _geminiService = new();

    [Fact]
    public async Task HelpStateHandler_ShouldUseDeterministicHelpAndNotCallGemini()
    {
        var handler = new HelpStateHandler(
            _geminiService.Object,
            NullLogger<HelpStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.Help };

        var response = await handler.HandleAsync(ctx, "giúp tôi");

        Assert.Contains("xem sản phẩm", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("theo dõi đơn hàng", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartReviewStateHandler_ShouldNotDisplayFakeTotal_WhenPricesAreNotResolved()
    {
        var handler = new CartReviewStateHandler(
            _geminiService.Object,
            Mock.Of<IProductRepository>(),
            NullLogger<CartReviewStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.CartReview };
        ctx.SetData("cartItems", new List<string> { "unknown-product", "unknown-product-2" });

        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("view_cart");

        var response = await handler.HandleAsync(ctx, "xem giỏ hàng");

        Assert.DoesNotContain("60đ", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("59.98", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cần kiểm tra", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrderConfirmationStateHandler_ShouldNotClaimDeliveryEstimateWithoutSource()
    {
        var handler = new OrderConfirmationStateHandler(
            _geminiService.Object,
            NullLogger<OrderConfirmationStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderConfirmation };

        var response = await handler.HandleAsync(ctx, "xác nhận");

        Assert.DoesNotContain("3-5 ngày", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shop sẽ xác nhận", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrderPlacedStateHandler_ShouldNotClaimPaymentOrFulfillmentStatusWithoutSource()
    {
        var handler = new OrderPlacedStateHandler(
            _geminiService.Object,
            NullLogger<OrderPlacedStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.OrderPlaced };
        ctx.SetData("orderId", "ORD-TEST");

        var response = await handler.HandleAsync(ctx, "theo dõi đơn hàng");

        Assert.DoesNotContain("Đã nhận thanh toán", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Đang chuẩn bị hàng", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Đơn hàng đã xác nhận", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shop sẽ cập nhật", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductDetailStateHandler_ShouldUseActiveTenantProductLookup()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(x => x.GetActiveByIdAsync("product-1", tenantId))
            .ReturnsAsync(new Product
            {
                Id = "product-1",
                TenantId = tenantId,
                Name = "Mặt Nạ Ngủ Dưỡng Ẩm",
                Variants = new List<ProductVariant>
                {
                    new() { Id = "variant-1", ProductId = "product-1", VolumeML = 50, Texture = "gel", Price = 320000m, StockQuantity = 3 }
                }
            });
        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("view_details");
        var handler = new ProductDetailStateHandler(
            _geminiService.Object,
            productRepository.Object,
            tenantContext,
            NullLogger<ProductDetailStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.ProductDetail };
        ctx.SetData("selectedProductId", "product-1");

        var response = await handler.HandleAsync(ctx, "xem chi tiết");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        productRepository.Verify(x => x.GetActiveByIdAsync("product-1", tenantId), Times.Once);
        productRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VariantSelectionStateHandler_ShouldUseActiveTenantProductLookup()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);

        var productRepository = new Mock<IProductRepository>();
        productRepository
            .Setup(x => x.GetActiveByIdAsync("product-1", tenantId))
            .ReturnsAsync(new Product
            {
                Id = "product-1",
                TenantId = tenantId,
                Name = "Mặt Nạ Ngủ Dưỡng Ẩm",
                Variants = new List<ProductVariant>
                {
                    new() { Id = "variant-1", ProductId = "product-1", VolumeML = 50, Texture = "gel", Price = 320000m, StockQuantity = 3 }
                }
            });
        var handler = new VariantSelectionStateHandler(
            _geminiService.Object,
            productRepository.Object,
            tenantContext,
            NullLogger<VariantSelectionStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.VariantSelection };
        ctx.SetData("selectedProductId", "product-1");

        var response = await handler.HandleAsync(ctx, "1");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("320", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("đ", response, StringComparison.OrdinalIgnoreCase);
        productRepository.Verify(x => x.GetActiveByIdAsync("product-1", tenantId), Times.Once);
        productRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BrowsingProductsStateHandler_ShouldSearchProductsWithinResolvedTenantOnly()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);
        var vectorSearchRepository = new Mock<IVectorSearchRepository>();
        var embeddingService = new Mock<IEmbeddingService>();
        var embedding = Enumerable.Repeat(0.1f, 768).ToArray();
        embeddingService.Setup(x => x.EmbedAsync("mặt nạ dưỡng ẩm", It.IsAny<CancellationToken>())).ReturnsAsync(embedding);
        vectorSearchRepository
            .Setup(x => x.SearchSimilarProductsAsync(embedding, tenantId, 5, It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new() { Id = "product-1", TenantId = tenantId, Name = "Mặt Nạ Ngủ Dưỡng Ẩm", Brand = "Test", BasePrice = 320000m, Description = "Dưỡng ẩm" }
            });
        var handler = new BrowsingProductsStateHandler(
            _geminiService.Object,
            vectorSearchRepository.Object,
            embeddingService.Object,
            tenantContext,
            NullLogger<BrowsingProductsStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.BrowsingProducts };

        var response = await handler.HandleAsync(ctx, "mặt nạ dưỡng ẩm");

        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        vectorSearchRepository.Verify(x => x.SearchSimilarProductsAsync(embedding, tenantId, 5, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SkinAnalysisStateHandler_ShouldNotEchoNonAllowlistedGeminiSkinType()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);
        var vectorSearchRepository = new Mock<IVectorSearchRepository>();
        var embeddingService = new Mock<IEmbeddingService>();
        var embedding = Enumerable.Repeat(0.1f, 768).ToArray();
        embeddingService.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(embedding);
        vectorSearchRepository
            .Setup(x => x.SearchSimilarProductsAsync(embedding, tenantId, 5, It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("da siêu trắng cần serum đặc trị 150.000đ");
        var handler = new SkinAnalysisStateHandler(
            _geminiService.Object,
            vectorSearchRepository.Object,
            embeddingService.Object,
            tenantContext,
            NullLogger<SkinAnalysisStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinAnalysis };

        var response = await handler.HandleAsync(ctx, "da tôi hơi khó mô tả");

        Assert.DoesNotContain("da siêu trắng", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("150.000", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cần kiểm tra", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SkinConsultationStateHandler_ShouldNotReturnFreeFormGeminiConsultationOrDollarPrices()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new NullTenantContext();
        tenantContext.Initialize(tenantId, "page-id", null);
        var vectorSearchRepository = new Mock<IVectorSearchRepository>();
        var embeddingService = new Mock<IEmbeddingService>();
        var embedding = Enumerable.Repeat(0.1f, 768).ToArray();
        embeddingService.Setup(x => x.EmbedAsync("da khô cần cấp ẩm", It.IsAny<CancellationToken>())).ReturnsAsync(embedding);
        vectorSearchRepository
            .Setup(x => x.SearchSimilarProductsAsync(embedding, tenantId, 3, It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>
            {
                new() { Id = "product-1", TenantId = tenantId, Name = "Mặt Nạ Ngủ Dưỡng Ẩm", BasePrice = 320000m }
            });
        _geminiService
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
                It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Nên dùng Mặt nạ gạo giá 150.000đ để trẻ hóa da.");
        var handler = new SkinConsultationStateHandler(
            _geminiService.Object,
            vectorSearchRepository.Object,
            embeddingService.Object,
            tenantContext,
            NullLogger<SkinConsultationStateHandler>.Instance);
        var ctx = new StateContext { FacebookPSID = "test-psid", CurrentState = ConversationState.SkinConsultation };

        var response = await handler.HandleAsync(ctx, "da khô cần cấp ẩm");

        Assert.DoesNotContain("Mặt nạ gạo", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("150.000", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$", response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mặt Nạ Ngủ Dưỡng Ẩm", response, StringComparison.OrdinalIgnoreCase);
        _geminiService.Verify(x => x.SendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<MessengerWebhook.Services.AI.Models.ConversationMessage>>(),
            It.IsAny<MessengerWebhook.Services.AI.Models.GeminiModelType?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
