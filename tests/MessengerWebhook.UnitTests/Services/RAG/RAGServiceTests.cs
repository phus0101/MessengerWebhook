using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.RAG;

public class RAGServiceTests
{
    private readonly Mock<IHybridSearchService> _mockHybridSearch;
    private readonly Mock<IContextAssembler> _mockContextAssembler;
    private readonly Mock<ILogger<RAGService>> _mockLogger;
    private readonly NullTenantContext _tenantContext;
    private readonly RAGOptions _ragOptions;
    private readonly RAGService _ragService;
    private readonly Guid _tenantId = Guid.NewGuid();

    public RAGServiceTests()
    {
        _mockHybridSearch = new Mock<IHybridSearchService>();
        _mockContextAssembler = new Mock<IContextAssembler>();
        _mockLogger = new Mock<ILogger<RAGService>>();
        _tenantContext = new NullTenantContext();
        _tenantContext.Initialize(_tenantId, "PAGE_1", null);
        _ragOptions = new RAGOptions
        {
            Enabled = true,
            TopK = 5,
            FallbackStrategy = "full-context",
            TimeoutMs = 5000
        };

        _ragService = new RAGService(
            _mockHybridSearch.Object,
            _mockContextAssembler.Object,
            _tenantContext,
            Options.Create(_ragOptions),
            _mockLogger.Object);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithResults_ReturnsFormattedContext()
    {
        // Arrange
        var query = "kem chống nắng";
        var searchResults = new List<FusedResult>
        {
            new FusedResult { ProductId = "product-1", RRFScore = 0.95 },
            new FusedResult { ProductId = "product-2", RRFScore = 0.85 }
        };
        var formattedContext = "Sản phẩm liên quan:\n1. Product 1\n2. Product 2";

        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 5, It.Is<Dictionary<string, object>>(f => f["tenant_id"].Equals(_tenantId.ToString())), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var products = new List<GroundedProduct>
        {
            new("product-1", "P1", "Product 1", "Cosmetics", 100000m),
            new("product-2", "P2", "Product 2", "Cosmetics", 200000m)
        };

        _mockContextAssembler
            .Setup(x => x.AssembleContextAsync(
                It.Is<List<string>>(ids => ids.Count == 2),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssembledRAGContext(formattedContext, new List<string> { "product-1", "product-2" }, products));

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5, includeDetailedInfo: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(formattedContext, result.FormattedContext);
        Assert.Equal(2, result.ProductIds.Count);
        Assert.Equal("product-1", result.ProductIds[0]);
        Assert.Equal("product-2", result.ProductIds[1]);
        Assert.Equal(2, result.Products.Count);
        Assert.Equal("Product 1", result.Products[0].Name);
        Assert.Equal(2, result.Metrics.ProductsRetrieved);
        Assert.Equal("hybrid", result.Metrics.Source);
    }

    [Fact]
    public async Task RetrieveContextAsync_NoResults_ReturnsEmptyContext()
    {
        // Arrange
        var query = "nonexistent product";
        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 5, It.Is<Dictionary<string, object>>(f => f["tenant_id"].Equals(_tenantId.ToString())), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5, includeDetailedInfo: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Không tìm thấy sản phẩm phù hợp.", result.FormattedContext);
        Assert.Empty(result.ProductIds);
        Assert.Equal(0, result.Metrics.ProductsRetrieved);
        Assert.Equal("empty", result.Metrics.Source);
    }

    [Fact]
    public async Task RetrieveContextAsync_WhenTenantIsNotResolved_ReturnsEmptyContext()
    {
        _tenantContext.Clear();

        var result = await _ragService.RetrieveContextAsync("kem chống nắng", topK: 5, includeDetailedInfo: false);

        Assert.Equal("Không tìm thấy sản phẩm phù hợp.", result.FormattedContext);
        Assert.Empty(result.ProductIds);
        Assert.Equal("empty", result.Metrics.Source);
        _mockHybridSearch.Verify(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveContextAsync_WhenAssemblerFiltersAllProducts_ReturnsEmptyProductIds()
    {
        var query = "mặt nạ";
        var searchResults = new List<FusedResult>
        {
            new FusedResult { ProductId = "inactive-product", RRFScore = 0.95 },
            new FusedResult { ProductId = "other-tenant-product", RRFScore = 0.85 }
        };

        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 5, It.Is<Dictionary<string, object>>(f => f["tenant_id"].Equals(_tenantId.ToString())), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContextAssembler
            .Setup(x => x.AssembleContextAsync(
                It.Is<List<string>>(ids => ids.Count == 2),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssembledRAGContext("Không tìm thấy sản phẩm phù hợp.", new List<string>(), new List<GroundedProduct>()));

        var result = await _ragService.RetrieveContextAsync(query, topK: 5);

        Assert.Equal("Không tìm thấy sản phẩm phù hợp.", result.FormattedContext);
        Assert.Empty(result.ProductIds);
        Assert.Equal(0, result.Metrics.ProductsRetrieved);
        Assert.Equal("empty", result.Metrics.Source);
    }

    [Fact]
    public async Task RetrieveContextAsync_SearchFails_ReturnsFallbackContext()
    {
        // Arrange
        var query = "test query";
        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 5, It.Is<Dictionary<string, object>>(f => f["tenant_id"].Equals(_tenantId.ToString())), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Search service unavailable"));

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5, includeDetailedInfo: false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Không tìm thấy sản phẩm phù hợp.", result.FormattedContext);
        Assert.Empty(result.ProductIds);
        Assert.Equal("fallback", result.Metrics.Source);
    }

    [Fact]
    public async Task RetrieveContextAsync_TracksMetrics()
    {
        // Arrange
        var query = "test";
        var searchResults = new List<FusedResult> { new FusedResult { ProductId = "p1", RRFScore = 0.9 } };

        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 3, It.Is<Dictionary<string, object>>(f => f["tenant_id"].Equals(_tenantId.ToString())), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContextAssembler
            .Setup(x => x.AssembleContextAsync(It.IsAny<List<string>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssembledRAGContext("Context", new List<string> { "p1" }, new List<GroundedProduct> { new("p1", "P1", "Product 1", "Cosmetics", 100000m) }));

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 3, includeDetailedInfo: false);

        // Assert
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.RetrievalLatency.TotalMilliseconds >= 0);
        Assert.True(result.Metrics.TotalLatency.TotalMilliseconds >= result.Metrics.RetrievalLatency.TotalMilliseconds);
        Assert.Equal(1, result.Metrics.ProductsRetrieved);
    }
}
