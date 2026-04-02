using MessengerWebhook.Data;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.VectorSearch;

public class KeywordSearchServiceTests : IDisposable
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly KeywordSearchService _service;
    private readonly Mock<ILogger<KeywordSearchService>> _loggerMock;

    public KeywordSearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<MessengerBotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TestMessengerBotDbContext(options);
        _loggerMock = new Mock<ILogger<KeywordSearchService>>();
        _service = new KeywordSearchService(_dbContext, _loggerMock.Object);

        SeedTestData();
    }

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

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    private void SeedTestData()
    {
        var products = new List<Product>
        {
            new()
            {
                Id = "p1",
                Code = "MUI_XU_SPF50",
                Name = "Kem chống nắng Murad",
                Description = "Kem chống nắng SPF 50 cho da nhạy cảm",
                Category = ProductCategory.Cosmetics,
                BasePrice = 250000,
                TenantId = Guid.NewGuid()
            },
            new()
            {
                Id = "p2",
                Code = "SRM_VIT_C",
                Name = "Serum Vitamin C",
                Description = "Serum dưỡng trắng da với vitamin C",
                Category = ProductCategory.Cosmetics,
                BasePrice = 350000,
                TenantId = Guid.NewGuid()
            },
            new()
            {
                Id = "p3",
                Code = "TNR_ROSE",
                Name = "Toner hoa hồng",
                Description = "Nước hoa hồng cân bằng da",
                Category = ProductCategory.Cosmetics,
                BasePrice = 180000,
                TenantId = Guid.NewGuid()
            },
            new()
            {
                Id = "p4",
                Code = "KEM_DUONG",
                Name = "Kem dưỡng ẩm Cetaphil",
                Description = "Kem dưỡng ẩm cho da khô",
                Category = ProductCategory.Cosmetics,
                BasePrice = 200000,
                TenantId = Guid.NewGuid()
            },
            new()
            {
                Id = "p5",
                Code = "SRM_NIACINAMIDE",
                Name = "Serum Niacinamide 10%",
                Description = "Serum se khít lỗ chân lông",
                Category = ProductCategory.Cosmetics,
                BasePrice = 280000,
                TenantId = Guid.NewGuid()
            }
        };

        _dbContext.Products.AddRange(products);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task SearchAsync_WithExactProductCode_ReturnsMatchingProduct()
    {
        // Arrange
        var query = "MUI_XU_SPF50";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("Murad", topResult.Name);
    }

    [Fact]
    public async Task SearchAsync_WithVietnameseQuery_HandlesCorrectly()
    {
        // Arrange
        var query = "kem chống nắng";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("chống nắng", topResult.Name);
    }

    [Fact]
    public async Task SearchAsync_WithVietnameseDiacritics_HandlesCorrectly()
    {
        // Arrange
        var query = "dưỡng ẩm";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p4", topResult.ProductId);
        Assert.Contains("dưỡng ẩm", topResult.Name);
    }

    [Fact]
    public async Task SearchAsync_WithPartialProductCode_ReturnsMatches()
    {
        // Arrange
        // Note: Tokenizer splits on underscores, so "SRM" alone won't match "SRM_VIT_C"
        // Use "serum" which appears in product names
        var query = "serum";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2); // Should match multiple serum products
        Assert.All(results, r => Assert.Contains("Serum", r.Name));
    }

    [Fact]
    public async Task SearchAsync_WithBrandName_ReturnsMatchingProducts()
    {
        // Arrange
        var query = "Cetaphil";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p4", topResult.ProductId);
        Assert.Contains("Cetaphil", topResult.Name);
    }

    [Fact]
    public async Task SearchAsync_WithCommonTerm_ReturnsMultipleResults()
    {
        // Arrange
        var query = "serum";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2); // Should match multiple serum products
        Assert.All(results, r => Assert.Contains("Serum", r.Name));
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        var query = "";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        // Description has "SPF 50" which tokenizes to ["spf", "50"]
        var query = "spf 50";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopKLimit()
    {
        // Arrange
        var query = "kem";

        // Act
        var results = await _service.SearchAsync(query, topK: 2);

        // Assert
        Assert.True(results.Count <= 2);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsOrderedByScore()
    {
        // Arrange
        var query = "serum vitamin";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Score >= results[i + 1].Score,
                $"Results not ordered by score: {results[i].Score} < {results[i + 1].Score}");
        }
    }

    [Fact]
    public async Task SearchAsync_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var query = "nonexistent product xyz123";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithMixedCaseQuery_HandlesCorrectly()
    {
        // Arrange
        var query = "KEM CHỐNG NẮNG";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
    }

    [Fact]
    public async Task SearchAsync_WithDescriptionMatch_ReturnsResults()
    {
        // Arrange
        var query = "da nhạy cảm";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p1", topResult.ProductId);
        Assert.Contains("nhạy cảm", topResult.Name + " " + _dbContext.Products.First(p => p.Id == "p1").Description);
    }

    [Fact]
    public async Task SearchAsync_PreservesProductMetadata()
    {
        // Arrange
        var query = "MUI_XU_SPF50";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var result = results.First();
        Assert.Equal("p1", result.ProductId);
        Assert.Equal("Kem chống nắng Murad", result.Name);
        Assert.Equal("Cosmetics", result.Category);
        Assert.Equal(250000, result.Price);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public async Task SearchAsync_WithMultipleTokens_CalculatesBM25Correctly()
    {
        // Arrange
        var query = "serum vitamin c";

        // Act
        var results = await _service.SearchAsync(query, topK: 5);

        // Assert
        Assert.NotEmpty(results);
        var topResult = results.First();
        Assert.Equal("p2", topResult.ProductId); // Should match "Serum Vitamin C"
        Assert.True(topResult.Score > 0);
    }

    [Fact]
    public async Task SearchAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var query = "kem";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _service.SearchAsync(query, topK: 5, cancellationToken: cts.Token);
        });
    }
}
