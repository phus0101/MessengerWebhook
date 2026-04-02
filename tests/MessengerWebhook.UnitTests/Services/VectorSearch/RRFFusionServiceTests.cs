using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MessengerWebhook.UnitTests.Services.VectorSearch;

public class RRFFusionServiceTests
{
    private readonly RRFFusionService _service;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<RRFFusionService>> _loggerMock;

    public RRFFusionServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<RRFFusionService>>();

        // Default k=60
        _configMock.Setup(c => c.GetSection("RRF:K").Value).Returns("60");

        _service = new RRFFusionService(_configMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Fuse_WithEmptyLists_ReturnsEmpty()
    {
        // Arrange
        var rankedLists = new List<List<ProductSearchResult>>();

        // Act
        var results = _service.Fuse(rankedLists, topK: 5);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Fuse_WithSingleList_ReturnsTopK()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f },
            new() { ProductId = "p2", Name = "Product 2", Category = "Cosmetics", Price = 200, Score = 0.8f },
            new() { ProductId = "p3", Name = "Product 3", Category = "Cosmetics", Price = 300, Score = 0.7f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 2);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("p1", results[0].ProductId);
        Assert.Equal("p2", results[1].ProductId);
    }

    [Fact]
    public void Fuse_CalculatesRRFScoreCorrectly()
    {
        // Arrange - k=60, so rank 1 should give 1/(60+1) = 0.01639...
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 5);

        // Assert
        Assert.Single(results);
        var expectedScore = 1.0 / (60 + 1); // 0.01639...
        Assert.Equal(expectedScore, results[0].RRFScore, precision: 5);
    }

    [Fact]
    public void Fuse_ItemsInBothLists_GetHigherScores()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f },
            new() { ProductId = "p2", Name = "Product 2", Category = "Cosmetics", Price = 200, Score = 0.8f }
        };

        var list2 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.95f },
            new() { ProductId = "p3", Name = "Product 3", Category = "Cosmetics", Price = 300, Score = 0.85f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1, list2 }, topK: 5);

        // Assert
        Assert.Equal(3, results.Count);

        // p1 appears in both lists at rank 1, so should have highest RRF score
        Assert.Equal("p1", results[0].ProductId);

        // p1 should have score = 1/(60+1) + 1/(60+1) = 2 * 0.01639 = 0.03278...
        var expectedP1Score = 2.0 / (60 + 1);
        Assert.Equal(expectedP1Score, results[0].RRFScore, precision: 5);

        // p2 and p3 appear in only one list each
        Assert.True(results[0].RRFScore > results[1].RRFScore);
        Assert.True(results[0].RRFScore > results[2].RRFScore);
    }

    [Fact]
    public void Fuse_PreservesProductMetadata()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Kem chống nắng", Category = "Cosmetics", Price = 250000, Score = 0.9f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 5);

        // Assert
        Assert.Single(results);
        Assert.Equal("p1", results[0].ProductId);
        Assert.Equal("Kem chống nắng", results[0].Name);
        Assert.Equal("Cosmetics", results[0].Category);
        Assert.Equal(250000, results[0].Price);
    }

    [Fact]
    public void Fuse_StoresSourceScoresAndRanks()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f },
            new() { ProductId = "p2", Name = "Product 2", Category = "Cosmetics", Price = 200, Score = 0.8f }
        };

        var list2 = new List<ProductSearchResult>
        {
            new() { ProductId = "p2", Name = "Product 2", Category = "Cosmetics", Price = 200, Score = 0.95f },
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.85f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1, list2 }, topK: 5);

        // Assert
        var p1Result = results.First(r => r.ProductId == "p1");
        var p2Result = results.First(r => r.ProductId == "p2");

        // Check source scores
        Assert.Equal(0.9f, p1Result.SourceScores["list_0"]);
        Assert.Equal(0.85f, p1Result.SourceScores["list_1"]);
        Assert.Equal(0.8f, p2Result.SourceScores["list_0"]);
        Assert.Equal(0.95f, p2Result.SourceScores["list_1"]);

        // Check source ranks (1-indexed)
        Assert.Equal(1, p1Result.SourceRanks["list_0"]);
        Assert.Equal(2, p1Result.SourceRanks["list_1"]);
        Assert.Equal(2, p2Result.SourceRanks["list_0"]);
        Assert.Equal(1, p2Result.SourceRanks["list_1"]);
    }

    [Fact]
    public void Fuse_RankPositionMatters_LowerRankHigherScore()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f },
            new() { ProductId = "p2", Name = "Product 2", Category = "Cosmetics", Price = 200, Score = 0.8f },
            new() { ProductId = "p3", Name = "Product 3", Category = "Cosmetics", Price = 300, Score = 0.7f }
        };

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 5);

        // Assert
        // Rank 1: 1/(60+1) = 0.01639
        // Rank 2: 1/(60+2) = 0.01613
        // Rank 3: 1/(60+3) = 0.01587
        Assert.True(results[0].RRFScore > results[1].RRFScore);
        Assert.True(results[1].RRFScore > results[2].RRFScore);
    }

    [Fact]
    public void Fuse_RespectsTopKLimit()
    {
        // Arrange
        var list1 = new List<ProductSearchResult>();
        for (int i = 1; i <= 20; i++)
        {
            list1.Add(new ProductSearchResult
            {
                ProductId = $"p{i}",
                Name = $"Product {i}",
                Category = "Cosmetics",
                Price = i * 100,
                Score = 1.0f - (i * 0.01f)
            });
        }

        // Act
        var results = _service.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 5);

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Fuse_WithDifferentRankings_MergesCorrectly()
    {
        // Arrange - Vector search prefers semantic similarity
        var vectorResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Kem dưỡng da", Category = "Cosmetics", Price = 200000, Score = 0.95f },
            new() { ProductId = "p2", Name = "Sữa rửa mặt", Category = "Cosmetics", Price = 150000, Score = 0.85f },
            new() { ProductId = "p3", Name = "Toner", Category = "Cosmetics", Price = 180000, Score = 0.75f }
        };

        // Keyword search prefers exact matches
        var keywordResults = new List<ProductSearchResult>
        {
            new() { ProductId = "p3", Name = "Toner", Category = "Cosmetics", Price = 180000, Score = 2.5f },
            new() { ProductId = "p1", Name = "Kem dưỡng da", Category = "Cosmetics", Price = 200000, Score = 1.8f },
            new() { ProductId = "p4", Name = "Serum", Category = "Cosmetics", Price = 300000, Score = 1.2f }
        };

        // Act
        var results = _service.Fuse(
            new List<List<ProductSearchResult>> { vectorResults, keywordResults },
            topK: 5);

        // Assert
        Assert.Equal(4, results.Count);

        // p1 and p3 appear in both lists, should rank higher
        var topTwoIds = results.Take(2).Select(r => r.ProductId).ToList();
        Assert.Contains("p1", topTwoIds);
        Assert.Contains("p3", topTwoIds);
    }

    [Fact]
    public void Fuse_WithCustomK_UsesCorrectParameter()
    {
        // Arrange - Create service with k=30
        var customConfigMock = new Mock<IConfiguration>();
        customConfigMock.Setup(c => c.GetSection("RRF:K").Value).Returns("30");
        var customService = new RRFFusionService(customConfigMock.Object, _loggerMock.Object);

        var list1 = new List<ProductSearchResult>
        {
            new() { ProductId = "p1", Name = "Product 1", Category = "Cosmetics", Price = 100, Score = 0.9f }
        };

        // Act
        var results = customService.Fuse(new List<List<ProductSearchResult>> { list1 }, topK: 5);

        // Assert
        var expectedScore = 1.0 / (30 + 1); // k=30
        Assert.Equal(expectedScore, results[0].RRFScore, precision: 5);
    }
}
