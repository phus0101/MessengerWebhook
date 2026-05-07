using MessengerWebhook.Data.Entities;
using MessengerWebhook.Data.Repositories;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services;

public class ProductMappingServiceTests
{
    private readonly Mock<IProductRepository> _mockProductRepository;
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly NullTenantContext _tenantContext;
    private readonly ProductMappingService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ProductMappingServiceTests()
    {
        _mockProductRepository = new Mock<IProductRepository>();
        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _tenantContext = new NullTenantContext();
        _tenantContext.Initialize(_tenantId, "PAGE_1", null);
        _service = new ProductMappingService(_mockProductRepository.Object, _mockHybridSearchService.Object, _tenantContext);
    }

    [Theory]
    [InlineData("PRODUCT_KCN", true)]
    [InlineData("PRODUCT_KL", true)]
    [InlineData("PRODUCT_COMBO_2", true)]
    [InlineData("PRODUCT_", false)]
    [InlineData("KCN", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidPayload_ShouldValidateCorrectly(string? payload, bool expected)
    {
        // Act
        var result = _service.IsValidPayload(payload!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_ValidPayload_ReturnsProduct()
    {
        // Arrange
        var product = new Product { Id = "1", Code = "KCN", Name = "Kem Chống Nắng", IsActive = true };
        _mockProductRepository.Setup(r => r.GetActiveByCodeAsync("KCN", _tenantId))
            .ReturnsAsync(product);

        // Act
        var result = await _service.GetProductByPayloadAsync("PRODUCT_KCN");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("KCN", result.Code);
        Assert.Equal("Kem Chống Nắng", result.Name);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_InvalidPayload_ReturnsNull()
    {
        // Act
        var result = await _service.GetProductByPayloadAsync("INVALID");

        // Assert
        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetActiveByCodeAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByPayloadAsync_ProductNotFound_ReturnsNull()
    {
        // Arrange
        _mockProductRepository.Setup(r => r.GetActiveByCodeAsync("NOTFOUND", _tenantId))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _service.GetProductByPayloadAsync("PRODUCT_NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProductByCodeAsync_ExistingCode_ReturnsProduct()
    {
        // Arrange
        var product = new Product { Id = "1", Code = "KL", Name = "Kem Lụa", IsActive = true };
        _mockProductRepository.Setup(r => r.GetByCodeAsync("KL"))
            .ReturnsAsync(product);

        // Act
        var result = await _service.GetProductByCodeAsync("KL");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("KL", result.Code);
    }

    [Fact]
    public async Task GetProductByCodeAsync_NonExistingCode_ReturnsNull()
    {
        // Arrange
        _mockProductRepository.Setup(r => r.GetByCodeAsync("NOTFOUND"))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _service.GetProductByCodeAsync("NOTFOUND");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("freeship")]
    [InlineData("2 sản phẩm")]
    [InlineData("combo")]
    public async Task GetProductByMessageAsync_AmbiguousPromoPhrases_DoNotAutoMapToCombo2(string message)
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync(message, 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        var result = await _service.GetProductByMessageAsync(message);

        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetByCodeAsync("COMBO_2"), Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_DirectProductCode_ReturnsActiveProductWithoutHybridSearch()
    {
        var product = new Product { Id = "mask-global", Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", IsActive = true, TenantId = null };
        _mockProductRepository.Setup(r => r.GetActiveByCodeAsync("MN", _tenantId))
            .ReturnsAsync(product);

        var result = await _service.GetProductByMessageAsync("MN");

        Assert.NotNull(result);
        Assert.Equal("mask-global", result.Id);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_SuggestionLineWithProductCode_ReturnsActiveProductWithoutHybridSearch()
    {
        var product = new Product { Id = "mask-global", Code = "MN", Name = "Mặt Nạ Ngủ Dưỡng Ẩm", IsActive = true, TenantId = null };
        _mockProductRepository.Setup(r => r.GetActiveByCodeAsync("MN", _tenantId))
            .ReturnsAsync(product);

        var result = await _service.GetProductByMessageAsync("1) Mặt Nạ Ngủ Dưỡng Ẩm (MN) - 320,000đ");

        Assert.NotNull(result);
        Assert.Equal("mask-global", result.Id);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_WhenTenantIsNotResolved_ShouldReturnNull()
    {
        _tenantContext.Clear();

        var result = await _service.GetProductByMessageAsync("kem chống nắng");

        Assert.Null(result);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProductByMessageAsync_FormerHardCodedPhrase_ShouldUseHybridSearch()
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync("kem chống nắng", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        var result = await _service.GetProductByMessageAsync("kem chống nắng");

        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetByCodeAsync("KCN"), Times.Never);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync("kem chống nắng", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductByMessageAsync_ShouldUseHybridSearch()
    {
        var product = new Product { Id = "tinh-chat-duong", Code = "TCD", Name = "Tinh Chất Dưỡng Da", IsActive = true };
        _mockProductRepository.Setup(r => r.GetActiveByIdAsync("tinh-chat-duong", _tenantId))
            .ReturnsAsync(product);

        var fusedResults = new List<FusedResult>
        {
            new FusedResult
            {
                ProductId = "tinh-chat-duong",
                Name = "Tinh Chất Dưỡng Da",
                Category = "Skincare",
                Price = 350000,
                RRFScore = 0.85
            }
        };

        _mockHybridSearchService
            .Setup(s => s.SearchAsync("tinh chất dưỡng da", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fusedResults);

        var result = await _service.GetProductByMessageAsync("tinh chất dưỡng da");

        Assert.NotNull(result);
        Assert.Equal("tinh-chat-duong", result.Id);
        Assert.Equal("TCD", result.Code);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync("tinh chất dưỡng da", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductByMessageAsync_HybridSearchReturnsEmpty_ShouldReturnNull()
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync("sản phẩm không tồn tại", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        var result = await _service.GetProductByMessageAsync("sản phẩm không tồn tại");

        Assert.Null(result);
        _mockHybridSearchService.Verify(
            s => s.SearchAsync("sản phẩm không tồn tại", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProductByMessageAsync_HybridSearchFails_ShouldReturnNullGracefully()
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Vector search service unavailable"));

        var result = await _service.GetProductByMessageAsync("sản phẩm bất kỳ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProductByMessageAsync_WhenProductLookupFails_ShouldRethrow()
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync("test", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult> { new FusedResult { ProductId = "product-id", Name = "Product" } });
        _mockProductRepository
            .Setup(r => r.GetActiveByIdAsync("product-id", _tenantId))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetProductByMessageAsync("test"));
    }

    [Fact]
    public async Task GetProductByMessageAsync_HybridSearchReturnsInactiveProduct_ShouldSkipAndUseNextActiveProduct()
    {
        var activeProduct = new Product { Id = "active-product", Code = "ACTIVE", Name = "Active Product", IsActive = true };
        _mockProductRepository.Setup(r => r.GetActiveByIdAsync("inactive-product", _tenantId))
            .ReturnsAsync((Product?)null);
        _mockProductRepository.Setup(r => r.GetActiveByIdAsync("active-product", _tenantId))
            .ReturnsAsync(activeProduct);

        var fusedResults = new List<FusedResult>
        {
            new FusedResult { ProductId = "inactive-product", Name = "Inactive Product", RRFScore = 0.9 },
            new FusedResult { ProductId = "active-product", Name = "Active Product", RRFScore = 0.8 }
        };

        _mockHybridSearchService
            .Setup(s => s.SearchAsync("dưỡng ẩm", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fusedResults);

        var result = await _service.GetProductByMessageAsync("dưỡng ẩm");

        Assert.NotNull(result);
        Assert.Equal("active-product", result.Id);
        _mockProductRepository.Verify(r => r.GetActiveByIdAsync("inactive-product", _tenantId), Times.Once);
        _mockProductRepository.Verify(r => r.GetActiveByIdAsync("active-product", _tenantId), Times.Once);
    }

    [Fact]
    public async Task GetProductByMessageAsync_WithTenantContext_ShouldLookupActiveProductInTenant()
    {
        var tenantId = _tenantId;

        var product = new Product { Id = "tenant-product", TenantId = tenantId, Code = "TENANT", Name = "Tenant Product", IsActive = true };
        _mockProductRepository.Setup(r => r.GetActiveByIdAsync("tenant-product", tenantId))
            .ReturnsAsync(product);

        _mockHybridSearchService
            .Setup(s => s.SearchAsync("tenant product", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult> { new FusedResult { ProductId = "tenant-product", Name = "Tenant Product" } });

        var result = await _service.GetProductByMessageAsync("tenant product");

        Assert.NotNull(result);
        Assert.Equal("tenant-product", result.Id);
        _mockProductRepository.Verify(r => r.GetActiveByIdAsync("tenant-product", tenantId), Times.Once);
    }

    [Fact]
    public async Task GetProductByMessageAsync_WhenSearchIsCancelled_ShouldRethrow()
    {
        _mockHybridSearchService
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.GetProductByMessageAsync("test"));
    }

    [Fact]
    public async Task GetProductByMessageAsync_HybridSearchReturnsInvalidProductId_ShouldReturnNull()
    {
        var fusedResults = new List<FusedResult>
        {
            new FusedResult
            {
                ProductId = "invalid-id",
                Name = "Product",
                RRFScore = 0.5
            }
        };

        _mockHybridSearchService
            .Setup(s => s.SearchAsync("test", 5, It.Is<Dictionary<string, object>>(filter => HasTenantFilter(filter)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fusedResults);

        _mockProductRepository.Setup(r => r.GetActiveByIdAsync("invalid-id", _tenantId))
            .ReturnsAsync((Product?)null);

        var result = await _service.GetProductByMessageAsync("test");

        Assert.Null(result);
        _mockProductRepository.Verify(r => r.GetActiveByIdAsync("invalid-id", _tenantId), Times.Once);
    }

    private bool HasTenantFilter(Dictionary<string, object>? filter)
    {
        return filter != null &&
               filter.TryGetValue("tenant_id", out var tenantId) &&
               tenantId.Equals(_tenantId.ToString());
    }
}
