using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;

namespace MessengerWebhook.UnitTests.Services.Cache;

public class EmbeddingCacheServiceTests
{
    private readonly Mock<IEmbeddingService> _mockInnerService;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ILogger<EmbeddingCacheService>> _mockLogger;
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly IConfiguration _configuration;
    private readonly EmbeddingCacheService _service;

    public EmbeddingCacheServiceTests()
    {
        _mockInnerService = new Mock<IEmbeddingService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockLogger = new Mock<ILogger<EmbeddingCacheService>>();
        _keyGenerator = new CacheKeyGenerator();

        var configDict = new Dictionary<string, string>
        {
            { "CacheTTL:EmbeddingSeconds", "3600" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        _service = new EmbeddingCacheService(
            _mockInnerService.Object,
            _mockCache.Object,
            _keyGenerator,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task EmbedAsync_CacheHit_ReturnsFromCache()
    {
        // Arrange
        var text = "Kem chống nắng";
        var cachedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var cachedJson = JsonSerializer.Serialize(cachedEmbedding);

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _service.EmbedAsync(text);

        // Assert
        Assert.Equal(cachedEmbedding, result);
        _mockInnerService.Verify(s => s.EmbedAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmbedAsync_CacheMiss_GeneratesAndCaches()
    {
        // Arrange
        var text = "Kem chống nắng";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.EmbedAsync(text, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        var result = await _service.EmbedAsync(text);

        // Assert
        Assert.Equal(embedding, result);
        _mockInnerService.Verify(s => s.EmbedAsync(text, It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbedAsync_CacheMiss_SetsCacheWithCorrectTTL()
    {
        // Arrange
        var text = "Kem chống nắng";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.EmbedAsync(text, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        DistributedCacheEntryOptions? capturedOptions = null;
        _mockCache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, options, _) => capturedOptions = options);

        // Act
        await _service.EmbedAsync(text);

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.Equal(TimeSpan.FromSeconds(3600), capturedOptions.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task EmbedBatchAsync_AllCached_ReturnsFromCache()
    {
        // Arrange
        var texts = new List<string> { "Kem chống nắng", "Sữa rửa mặt" };
        var embeddings = new List<float[]>
        {
            new float[] { 0.1f, 0.2f, 0.3f },
            new float[] { 0.4f, 0.5f, 0.6f }
        };

        var callCount = 0;
        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                var result = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(embeddings[callCount]));
                callCount++;
                return result;
            });

        // Act
        var result = await _service.EmbedBatchAsync(texts);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(embeddings[0], result[0]);
        Assert.Equal(embeddings[1], result[1]);
        _mockInnerService.Verify(s => s.EmbedBatchAsync(
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmbedBatchAsync_PartiallyCached_GeneratesOnlyMissing()
    {
        // Arrange
        var texts = new List<string> { "Kem chống nắng", "Sữa rửa mặt", "Serum" };
        var cachedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var generatedEmbeddings = new List<float[]>
        {
            new float[] { 0.4f, 0.5f, 0.6f },
            new float[] { 0.7f, 0.8f, 0.9f }
        };

        var getCallCount = 0;
        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                // First call is for "Kem chống nắng" - return cached
                if (getCallCount == 0)
                {
                    getCallCount++;
                    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cachedEmbedding));
                }
                // Other calls are cache misses
                getCallCount++;
                return null;
            });

        _mockInnerService.Setup(s => s.EmbedBatchAsync(
            It.Is<List<string>>(l => l.Count == 2),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(generatedEmbeddings);

        // Act
        var result = await _service.EmbedBatchAsync(texts);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(cachedEmbedding, result[0]);
        Assert.Equal(generatedEmbeddings[0], result[1]);
        Assert.Equal(generatedEmbeddings[1], result[2]);
        _mockInnerService.Verify(s => s.EmbedBatchAsync(
            It.Is<List<string>>(l => l.Count == 2 && l[0] == "Sữa rửa mặt" && l[1] == "Serum"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbedBatchAsync_NoCached_GeneratesAll()
    {
        // Arrange
        var texts = new List<string> { "Kem chống nắng", "Sữa rửa mặt" };
        var embeddings = new List<float[]>
        {
            new float[] { 0.1f, 0.2f, 0.3f },
            new float[] { 0.4f, 0.5f, 0.6f }
        };

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.EmbedBatchAsync(texts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        // Act
        var result = await _service.EmbedBatchAsync(texts);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(embeddings[0], result[0]);
        Assert.Equal(embeddings[1], result[1]);
        _mockInnerService.Verify(s => s.EmbedBatchAsync(texts, It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var texts = new List<string>();

        // Act
        var result = await _service.EmbedBatchAsync(texts);

        // Assert
        Assert.Empty(result);
        _mockInnerService.Verify(s => s.EmbedBatchAsync(
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
