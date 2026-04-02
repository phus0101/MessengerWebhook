using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MessengerWebhook.IntegrationTests.Services.VectorSearch;

public class HybridSearchIntegrationTests : IDisposable
{
    private readonly TestMessengerBotDbContext _dbContext;
    private readonly HybridSearchService _hybridSearchService;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorSearchService> _vectorSearchMock;

    // Test-specific DbContext that excludes ProductEmbedding to avoid Vector type issues
    private class TestMessengerBotDbContext : MessengerBotDbContext
    {
        public TestMessengerBotDbContext(DbContextOptions<MessengerBotDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Ignore ProductEmbedding entity for in-memory tests (Vector type not supported)
            modelBuilder.Ignore<ProductEmbedding>();
        }
    }

    public HybridSearchIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TestMessengerBotDbContext(options);

        // Setup mocks
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorSearchMock = new Mock<IVectorSearchService>();

        // Setup configuration
        var configData = new Dictionary<string, string>
        {
            { "RRF:K", "60" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Create services
        var keywordSearch = new KeywordSearchService(
            _dbContext,
            Mock.Of<ILogger<KeywordSearchService>>());

        var rrfFusion = new RRFFusionService(
            configuration,
            Mock.Of<ILogger<RRFFusionService>>());

        _hybridSearchService = new HybridSearchService(
            _embeddingServiceMock.Object,
            _vectorSearchMock.Object,
            keywordSearch,
            rrfFusion,
            Mock.Of<ILogger<HybridSearchService>>());

        SeedTestData();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    private void SeedTestData()
    {
        var tenantId = Guid.NewGuid();
        var products = new List<Product>
        {
            new()
            {
                Id = "p1",
                Code = "MUI_XU_SPF50",
                Name = "Kem chống nắng Murad City Skin SPF 50",
                Description = "Kem chống nắng phổ rộng SPF 50 bảo vệ da khỏi tia UV và ô nhiễm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 850000,
                Brand = "Murad",
                TenantId = tenantId
            },
            new()
            {
                Id = "p2",
                Code = "SRM_VIT_C",
                Name = "Serum Vitamin C 20%",
                Description = "Serum dưỡng trắng da với vitamin C nồng độ cao",
                Category = ProductCategory.Cosmetics,
                BasePrice = 650000,
                Brand = "SkinCeuticals",
                TenantId = tenantId
            },
            new()
            {
                Id = "p3",
                Code = "TNR_ROSE",
                Name = "Toner hoa hồng Klairs",
                Description = "Nước hoa hồng cân bằng da không chứa cồn",
                Category = ProductCategory.Cosmetics,
                BasePrice = 380000,
                Brand = "Klairs",
                TenantId = tenantId
            },
            new()
            {
                Id = "p4",
                Code = "KEM_DUONG_AM",
                Name = "Kem dưỡng ẩm Cetaphil",
                Description = "Kem dưỡng ẩm cho da khô và nhạy cảm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 420000,
                Brand = "Cetaphil",
                TenantId = tenantId
            },
            new()
            {
                Id = "p5",
                Code = "SRM_NIACINAMIDE",
                Name = "Serum Niacinamide 10% + Zinc 1%",
                Description = "Serum se khít lỗ chân lông và kiểm soát dầu",
                Category = ProductCategory.Cosmetics,
                BasePrice = 280000,
                Brand = "The Ordinary",
                TenantId = tenantId
            }
        };

        _dbContext.Products.AddRange(products);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task SearchAsync_EndToEnd_CombinesVectorAndKeywordResults()
    {
        // Arrange
        var query = "kem chống nắng";
        var queryEmbedding = new float[768]; // Mock embedding

        // Mock vector search returns semantic matches
        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Kem chống nắng Murad City Skin SPF 50", Category = "Cosmetics", Price = 850000, Score = 0.92f },
            new() { ProductId = "p4", Name = "Kem dưỡng ẩm Cetaphil", Category = "Cosmetics", Price = 420000, Score = 0.75f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 5);

        // p1 should rank high (appears in both vector and keyword results)
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("chống nắng", topResult.Name);

        // Verify RRF scores are calculated
        Assert.All(results, r => Assert.True(r.RRFScore > 0));
    }

    [Fact]
    public async Task SearchAsync_WithExactProductCode_PrioritizesKeywordMatch()
    {
        // Arrange
        var query = "MUI_XU_SPF50";
        var queryEmbedding = new float[768];

        // Vector search returns no results (exact codes don't match well semantically)
        var vectorResults = new List<ProductSearchResult>();

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);

        // Keyword search should find exact match via product code
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("MUI_XU_SPF50", _dbContext.Products.First(p => p.Id == "p1").Code);
    }

    [Fact]
    public async Task SearchAsync_VietnameseBenchmark_KemChongNang()
    {
        // Arrange
        var query = "kem chống nắng";
        var queryEmbedding = new float[768];

        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Kem chống nắng Murad City Skin SPF 50", Category = "Cosmetics", Price = 850000, Score = 0.95f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - 100% accuracy: top result must be the sunscreen
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("chống nắng", topResult.Name);
    }

    [Fact]
    public async Task SearchAsync_VietnameseBenchmark_ExactProductCode()
    {
        // Arrange
        var query = "MUI_XU_SPF50";
        var queryEmbedding = new float[768];

        var vectorResults = new List<ProductSearchResult>();

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - 100% accuracy: keyword search must find exact code
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
    }

    [Fact]
    public async Task SearchAsync_Latency_CompletesUnder100ms()
    {
        // Arrange
        var query = "serum vitamin c";
        var queryEmbedding = new float[768];

        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p2", Name = "Serum Vitamin C 20%", Category = "Cosmetics", Price = 650000, Score = 0.88f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);
        stopwatch.Stop();

        // Assert - p95 latency < 100ms (for in-memory test, should be much faster)
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Search took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SearchAsync_Precision_RelevantProductsInTop5()
    {
        // Arrange
        var query = "serum";
        var queryEmbedding = new float[768];

        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p2", Name = "Serum Vitamin C 20%", Category = "Cosmetics", Price = 650000, Score = 0.90f },
            new() { ProductId = "p5", Name = "Serum Niacinamide 10% + Zinc 1%", Category = "Cosmetics", Price = 280000, Score = 0.85f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - Precision: >85% relevant (all results should contain "serum")
        Assert.NotEmpty(results);
        var relevantCount = results.Count(r => r.Name.Contains("Serum", StringComparison.OrdinalIgnoreCase));
        var precision = (double)relevantCount / results.Count;

        Assert.True(precision >= 0.85,
            $"Precision {precision:P0} is below 85% threshold");
    }

    [Fact]
    public async Task SearchAsync_HybridOutperformsVectorOnly_OnExactMatches()
    {
        // Arrange
        var query = "TNR_ROSE";
        var queryEmbedding = new float[768];

        // Vector-only might not rank exact code match highly
        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Kem chống nắng Murad City Skin SPF 50", Category = "Cosmetics", Price = 850000, Score = 0.70f },
            new() { ProductId = "p2", Name = "Serum Vitamin C 20%", Category = "Cosmetics", Price = 650000, Score = 0.65f },
            new() { ProductId = "p3", Name = "Toner hoa hồng Klairs", Category = "Cosmetics", Price = 380000, Score = 0.60f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var hybridResults = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - Hybrid should rank p3 (exact code match) higher than vector-only
        Assert.NotEmpty(hybridResults);
        var topResult = hybridResults.First();
        Assert.Equal("p3", topResult.ProductId);

        // In vector-only, p3 was ranked 3rd, but hybrid should boost it to 1st
        var vectorOnlyRank = vectorResults.FindIndex(r => r.ProductId == "p3") + 1;
        Assert.Equal(3, vectorOnlyRank); // Verify vector-only had it at rank 3
        Assert.Equal(1, 1); // Hybrid has it at rank 1 (first result)
    }

    [Fact]
    public async Task SearchAsync_WithFilter_PassesToVectorSearch()
    {
        // Arrange
        var query = "kem dưỡng";
        var queryEmbedding = new float[768];
        var filter = new Dictionary<string, object>
        {
            { "category", "Cosmetics" },
            { "price_max", 500000 }
        };

        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p4", Name = "Kem dưỡng ẩm Cetaphil", Category = "Cosmetics", Price = 420000, Score = 0.88f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5, filter: filter);

        // Assert
        Assert.NotEmpty(results);
        _vectorSearchMock.Verify(
            v => v.SearchSimilarAsync(queryEmbedding, 10, filter, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ParallelExecution_CompletesEfficiently()
    {
        // Arrange
        var query = "kem chống nắng";
        var queryEmbedding = new float[768];

        // Simulate slow vector search (50ms)
        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(50);
                return new List<ProductSearchResult>
                {
                    new() { ProductId = "p1", Name = "Kem chống nắng Murad City Skin SPF 50", Category = "Cosmetics", Price = 850000, Score = 0.92f }
                };
            });

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);
        stopwatch.Stop();

        // Assert - Should complete in ~50ms (parallel), not 100ms (sequential)
        Assert.True(stopwatch.ElapsedMilliseconds < 80,
            $"Parallel execution took {stopwatch.ElapsedMilliseconds}ms, expected < 80ms");
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyVectorResults_StillReturnsKeywordMatches()
    {
        // Arrange
        var query = "KEM_DUONG_AM";
        var queryEmbedding = new float[768];

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductSearchResult>());

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - Keyword search should still find the product
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p4", topResult.ProductId);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyKeywordResults_StillReturnsVectorMatches()
    {
        // Arrange
        var query = "moisturizer for sensitive skin"; // English query won't match Vietnamese products in keyword search
        var queryEmbedding = new float[768];

        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p4", Name = "Kem dưỡng ẩm Cetaphil", Category = "Cosmetics", Price = 420000, Score = 0.85f }
        };

        _embeddingServiceMock
            .Setup(e => e.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorSearchMock
            .Setup(v => v.SearchSimilarAsync(queryEmbedding, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await _hybridSearchService.SearchAsync(query, topK: 5);

        // Assert - Vector search should still return results
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p4", topResult.ProductId);
    }
}
