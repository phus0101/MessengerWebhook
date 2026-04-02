using MessengerWebhook.Configuration;
using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.RAG;
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
    private readonly RAGOptions _ragOptions;
    private readonly RAGService _ragService;

    public RAGServiceTests()
    {
        _mockHybridSearch = new Mock<IHybridSearchService>();
        _mockContextAssembler = new Mock<IContextAssembler>();
        _mockLogger = new Mock<ILogger<RAGService>>();
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
            .Setup(x => x.SearchAsync(query, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContextAssembler
            .Setup(x => x.AssembleContextAsync(
                It.Is<List<string>>(ids => ids.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(formattedContext);

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(formattedContext, result.FormattedContext);
        Assert.Equal(2, result.ProductIds.Count);
        Assert.Equal("product-1", result.ProductIds[0]);
        Assert.Equal("product-2", result.ProductIds[1]);
        Assert.Equal(2, result.Metrics.ProductsRetrieved);
        Assert.Equal("hybrid", result.Metrics.Source);
    }

    [Fact]
    public async Task RetrieveContextAsync_NoResults_ReturnsEmptyContext()
    {
        // Arrange
        var query = "nonexistent product";
        _mockHybridSearch
            .Setup(x => x.SearchAsync(query, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5);

        // Assert
        Assert.NotNull(result);
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
            .Setup(x => x.SearchAsync(query, 5, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Search service unavailable"));

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 5);

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
            .Setup(x => x.SearchAsync(query, 3, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockContextAssembler
            .Setup(x => x.AssembleContextAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Context");

        // Act
        var result = await _ragService.RetrieveContextAsync(query, topK: 3);

        // Assert
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.RetrievalLatency.TotalMilliseconds >= 0);
        Assert.True(result.Metrics.TotalLatency.TotalMilliseconds >= result.Metrics.RetrievalLatency.TotalMilliseconds);
        Assert.Equal(1, result.Metrics.ProductsRetrieved);
    }
}
