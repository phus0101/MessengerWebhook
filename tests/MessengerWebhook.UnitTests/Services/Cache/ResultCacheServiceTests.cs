using MessengerWebhook.Services.Cache;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;

namespace MessengerWebhook.UnitTests.Services.Cache;

public class ResultCacheServiceTests
{
    private readonly Mock<IHybridSearchService> _mockInnerService;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<ResultCacheService>> _mockLogger;
    private readonly CacheKeyGenerator _keyGenerator;
    private readonly IConfiguration _configuration;
    private readonly ResultCacheService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ResultCacheServiceTests()
    {
        _mockInnerService = new Mock<IHybridSearchService>();
        _mockCache = new Mock<IDistributedCache>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<ResultCacheService>>();
        _keyGenerator = new CacheKeyGenerator();

        var configDict = new Dictionary<string, string>
        {
            { "CacheTTL:ResultSeconds", "900" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        _mockTenantContext.Setup(t => t.TenantId).Returns(_tenantId);

        _service = new ResultCacheService(
            _mockInnerService.Object,
            _mockCache.Object,
            _keyGenerator,
            _mockTenantContext.Object,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SearchAsync_CacheHit_ReturnsFromCache()
    {
        // Arrange
        var query = "Kem chống nắng";
        var cachedResults = new List<FusedResult>
        {
            new FusedResult
            {
                ProductId = "prod1",
                Name = "Kem chống nắng A",
                Category = "Skincare",
                Price = 250000,
                RRFScore = 0.95
            }
        };
        var cachedJson = JsonSerializer.Serialize(cachedResults);

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _service.SearchAsync(query);

        // Assert
        Assert.Single(result);
        Assert.Equal("prod1", result[0].ProductId);
        _mockInnerService.Verify(s => s.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_CacheMiss_ExecutesSearchAndCaches()
    {
        // Arrange
        var query = "Kem chống nắng";
        var searchResults = new List<FusedResult>
        {
            new FusedResult
            {
                ProductId = "prod1",
                Name = "Kem chống nắng A",
                Category = "Skincare",
                Price = 250000,
                RRFScore = 0.95
            }
        };

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.SearchAsync(
            query,
            5,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _service.SearchAsync(query);

        // Assert
        Assert.Single(result);
        Assert.Equal("prod1", result[0].ProductId);
        _mockInnerService.Verify(s => s.SearchAsync(
            query, 5, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_CacheMiss_SetsCacheWithCorrectTTL()
    {
        // Arrange
        var query = "Kem chống nắng";
        var searchResults = new List<FusedResult>
        {
            new FusedResult { ProductId = "prod1", Name = "Test", Category = "Skincare", Price = 100000, RRFScore = 0.9 }
        };

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        DistributedCacheEntryOptions? capturedOptions = null;
        _mockCache.Setup(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, options, _) => capturedOptions = options);

        // Act
        await _service.SearchAsync(query);

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.Equal(TimeSpan.FromSeconds(900), capturedOptions.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task SearchAsync_DifferentTenants_UseDifferentCacheKeys()
    {
        // Arrange
        var query = "Kem chống nắng";
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var capturedKeys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKeys.Add(key))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        // Act - Tenant 1
        _mockTenantContext.Setup(t => t.TenantId).Returns(tenant1Id);
        await _service.SearchAsync(query);

        // Act - Tenant 2
        _mockTenantContext.Setup(t => t.TenantId).Returns(tenant2Id);
        await _service.SearchAsync(query);

        // Assert
        Assert.Equal(2, capturedKeys.Count);
        Assert.NotEqual(capturedKeys[0], capturedKeys[1]);
    }

    [Fact]
    public async Task SearchAsync_WithFilter_IncludesFilterInCacheKey()
    {
        // Arrange
        var query = "Kem chống nắng";
        var filter = new Dictionary<string, object> { { "category", "skincare" } };

        var capturedKeys = new List<string>();
        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedKeys.Add(key))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        // Act - Without filter
        await _service.SearchAsync(query);

        // Act - With filter
        await _service.SearchAsync(query, 5, filter);

        // Assert
        Assert.Equal(2, capturedKeys.Count);
        Assert.NotEqual(capturedKeys[0], capturedKeys[1]);
    }

    [Fact]
    public async Task SearchAsync_CustomTopK_PassedToInnerService()
    {
        // Arrange
        var query = "Kem chống nắng";
        var topK = 10;

        _mockCache.Setup(c => c.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mockInnerService.Setup(s => s.SearchAsync(
            query,
            topK,
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FusedResult>());

        // Act
        await _service.SearchAsync(query, topK);

        // Assert
        _mockInnerService.Verify(s => s.SearchAsync(
            query, topK, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
