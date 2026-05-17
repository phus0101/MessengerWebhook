using System.Net;
using System.Text;
using System.Text.Json;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Embeddings;
using MessengerWebhook.Services.RAG.Reranking;
using MessengerWebhook.Services.Tenants;
using MessengerWebhook.Services.VectorSearch;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.RAG;

public class RerankingTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private CohereRerankService BuildService(
        IHttpClientFactory factory,
        IDistributedCache? cache = null,
        CohereOptions? options = null)
    {
        var tenantCtx = new NullTenantContext();
        tenantCtx.Initialize(_tenantId, "PAGE_1", null);

        return new CohereRerankService(
            factory,
            cache ?? Mock.Of<IDistributedCache>(),
            tenantCtx,
            Options.Create(options ?? new CohereOptions()),
            Mock.Of<ILogger<CohereRerankService>>());
    }

    private static IHttpClientFactory BuildFactory(string jsonResponse, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(jsonResponse, status);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("cohere")).Returns(client);
        return factory.Object;
    }

    private static IHttpClientFactory BuildFailingFactory(Exception ex)
    {
        var handler = new MockHttpMessageHandler((_, _) => throw ex);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("cohere")).Returns(client);
        return factory.Object;
    }

    // ─── CohereRerankService tests ────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_CandidatesLteTopN_ReturnsAllWithScore1()
    {
        var svc = BuildService(Mock.Of<IHttpClientFactory>());
        var candidates = new List<RankableDocument>
        {
            new("a", "text a"),
            new("b", "text b"),
            new("c", "text c")
        };

        var result = await svc.RerankAsync("query", candidates, topN: 5);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(1.0, r.RelevanceScore));
        // No HTTP call should be made (factory not configured — would throw if called)
    }

    [Fact]
    public async Task RerankAsync_CacheHit_SkipsApiCall()
    {
        var cached = JsonSerializer.Serialize(new List<RankedDocument>
        {
            new("doc1", "text 1", 0.99),
            new("doc2", "text 2", 0.88)
        });

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(cached));

        // Factory that throws if called — proves cache was hit
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Throws(new InvalidOperationException("HTTP should not be called on cache hit"));

        var svc = BuildService(factoryMock.Object, cacheMock.Object);
        var candidates = new List<RankableDocument>
        {
            new("doc1", "text 1"),
            new("doc2", "text 2"),
            new("doc3", "text 3"),
            new("doc4", "text 4")
        };

        var result = await svc.RerankAsync("query", candidates, topN: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("doc1", result[0].Id);
        Assert.Equal(0.99, result[0].RelevanceScore);
    }

    [Fact]
    public async Task RerankAsync_ApiSuccess_ReturnsRerankedOrder()
    {
        // Cohere returns: index 2 (highest) → index 0 → index 1
        var apiResponse = """
            {
              "results": [
                { "index": 2, "relevance_score": 0.98 },
                { "index": 0, "relevance_score": 0.72 }
              ]
            }
            """;

        var svc = BuildService(BuildFactory(apiResponse));
        var candidates = new List<RankableDocument>
        {
            new("a", "text a"),
            new("b", "text b"),
            new("c", "text c")
        };

        var result = await svc.RerankAsync("query", candidates, topN: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("c", result[0].Id);   // index 2
        Assert.Equal(0.98, result[0].RelevanceScore);
        Assert.Equal("a", result[1].Id);   // index 0
        Assert.Equal(0.72, result[1].RelevanceScore);
    }

    [Fact]
    public async Task RerankAsync_ApiFailure_ReturnsFallbackOrder()
    {
        var svc = BuildService(BuildFailingFactory(new HttpRequestException("connection refused")));
        var candidates = new List<RankableDocument>
        {
            new("x", "first"),
            new("y", "second"),
            new("z", "third")
        };

        var result = await svc.RerankAsync("query", candidates, topN: 2);

        // Should return first topN in original order with score=0
        Assert.Equal(2, result.Count);
        Assert.Equal("x", result[0].Id);
        Assert.Equal("y", result[1].Id);
        Assert.All(result, r => Assert.Equal(0.0, r.RelevanceScore));
    }

    // ─── HybridSearchService rerank integration tests ─────────────────────────

    private static HybridSearchService BuildHybridService(
        Mock<IEmbeddingService> embeddingMock,
        Mock<IVectorSearchService> vectorMock,
        Mock<IRerankService> rerankMock,
        bool rerankEnabled,
        IConfiguration? config = null)
    {
        config ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { { "RRF:K", "60" } }!)
            .Build();

        var tenantCtx = new NullTenantContext();
        tenantCtx.Initialize(Guid.NewGuid(), "PAGE_1", null);

        // Minimal in-memory DB for KeywordSearchService
        var dbOptions = new DbContextOptionsBuilder<MessengerWebhook.Data.MessengerBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MessengerWebhook.Data.MessengerBotDbContext(dbOptions);

        var keywordSearch = new KeywordSearchService(db, tenantCtx, Mock.Of<ILogger<KeywordSearchService>>());
        var rrfFusion = new RRFFusionService(config, Mock.Of<ILogger<RRFFusionService>>());

        return new HybridSearchService(
            embeddingMock.Object,
            vectorMock.Object,
            keywordSearch,
            rrfFusion,
            Mock.Of<ILogger<HybridSearchService>>(),
            rerankMock.Object,
            Options.Create(new CohereOptions { Enabled = rerankEnabled, CandidateMultiplier = 4 }));
    }

    [Fact]
    public async Task HybridSearch_RerankEnabled_CallsRerankAfterFusion()
    {
        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorMock = new Mock<IVectorSearchService>();
        var rerankMock = new Mock<IRerankService>();

        embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        // Return enough vector results to trigger rerank (fusedResults.Count > topK=2)
        var vectorResults = Enumerable.Range(0, 6)
            .Select(i => new ProductSearchResult
            {
                ProductId = $"p{i}", Name = $"Product {i}", Category = "Cat", Price = 100, Score = 0.9f - i * 0.05f
            })
            .ToList();

        vectorMock
            .Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Rerank returns top 2
        rerankMock
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RankableDocument>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RankedDocument>
            {
                new("p3", "Product 3 Cat", 0.95),
                new("p1", "Product 1 Cat", 0.80)
            });

        var svc = BuildHybridService(embeddingMock, vectorMock, rerankMock, rerankEnabled: true);

        await svc.SearchAsync("query", topK: 2);

        rerankMock.Verify(
            r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RankableDocument>>(), 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HybridSearch_RerankDisabled_SkipsRerank()
    {
        var embeddingMock = new Mock<IEmbeddingService>();
        var vectorMock = new Mock<IVectorSearchService>();
        var rerankMock = new Mock<IRerankService>();

        embeddingMock
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        vectorMock
            .Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductSearchResult>
            {
                new() { ProductId = "p1", Name = "P1", Category = "Cat", Price = 100, Score = 0.9f }
            });

        var svc = BuildHybridService(embeddingMock, vectorMock, rerankMock, rerankEnabled: false);

        await svc.SearchAsync("query", topK: 5);

        rerankMock.Verify(
            r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RankableDocument>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
