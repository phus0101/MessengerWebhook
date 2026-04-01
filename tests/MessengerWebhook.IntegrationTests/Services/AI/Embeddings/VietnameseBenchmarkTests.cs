using System.Diagnostics;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.IntegrationTests.Services.AI.Embeddings;

/// <summary>
/// Vietnamese cosmetics benchmark tests
/// Target: 100% accuracy on 13 Vietnamese queries
/// Based on research report: researcher-260401-1311-vietnamese-embedding-benchmark.md
/// </summary>
public class VietnameseBenchmarkTests : IDisposable
{
    private readonly IEmbeddingService _service;
    private readonly ILogger<VertexAIEmbeddingService> _logger;
    private readonly bool _skipTests;
    private readonly List<Product> _products;

    public VietnameseBenchmarkTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection("VertexAI").Get<VertexAIOptions>();

        if (options == null || string.IsNullOrEmpty(options.ServiceAccountKeyPath) ||
            !File.Exists(options.ServiceAccountKeyPath))
        {
            _skipTests = true;
            _service = null!;
            _logger = null!;
            _products = new List<Product>();
            return;
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = loggerFactory.CreateLogger<VertexAIEmbeddingService>();

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        _service = new VertexAIEmbeddingService(
            httpClient,
            Options.Create(options),
            _logger);

        _products = GetTestProducts();
    }

    [Theory]
    [InlineData("kem chống nắng cho da dầu", "Kem chống nắng vật lý Múi Xù")]
    [InlineData("kem chong nang", "Kem chống nắng vật lý Múi Xù")] // No diacritics
    [InlineData("sữa rửa mặt cho da nhờn", "Sữa rửa mặt CeraVe")]
    [InlineData("serum vitamin C", "Serum Vitamin C The Ordinary")]
    [InlineData("toner cân bằng pH", "Toner Some By Mi AHA BHA PHA")]
    [InlineData("kem dưỡng ẩm ban đêm", "Kem dưỡng ẩm Cetaphil")]
    [InlineData("mặt nạ ngủ", "Mặt nạ ngủ Laneige")]
    [InlineData("tinh chất chống lão hóa", "Tinh chất Estée Lauder Advanced Night Repair")]
    [InlineData("kem mắt trà xanh", "Kem mắt Innisfree Green Tea")]
    [InlineData("xịt khoáng", "Xịt khoáng Avène")]
    [InlineData("dầu tẩy trang", "Dầu tẩy trang DHC")]
    [InlineData("kem nền che phủ cao", "Kem nền Maybelline Fit Me")]
    [InlineData("son môi lâu trôi", "Son môi MAC Ruby Woo")]
    public async Task SemanticSearch_VietnameseQuery_FindsCorrectProduct(
        string query,
        string expectedProductName)
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act - Generate query embedding
        var queryEmbedding = await _service.EmbedAsync(query);

        // Generate product embeddings in batch
        var productDescriptions = _products.Select(p => p.Description).ToList();
        var productEmbeddings = await _service.EmbedBatchAsync(productDescriptions);

        stopwatch.Stop();

        // Calculate similarities
        var results = _products
            .Select((product, idx) => new
            {
                Product = product,
                Similarity = CosineSimilarity(queryEmbedding, productEmbeddings[idx])
            })
            .OrderByDescending(x => x.Similarity)
            .ToList();

        // Assert
        var topResult = results.First();
        topResult.Product.Name.Should().Contain(expectedProductName,
            $"Query '{query}' should find '{expectedProductName}' as top result");

        topResult.Similarity.Should().BeGreaterThan(0.6,
            "Top result similarity should be above threshold");

        _logger.LogInformation(
            "Query: '{Query}' → Top: '{Product}' (similarity: {Sim:F4}, latency: {Ms}ms)",
            query, topResult.Product.Name, topResult.Similarity, stopwatch.ElapsedMilliseconds);

        // Log top 3 results for debugging
        foreach (var result in results.Take(3))
        {
            _logger.LogDebug(
                "  {Rank}. {Product} (similarity: {Sim:F4})",
                results.IndexOf(result) + 1,
                result.Product.Name,
                result.Similarity);
        }
    }

    [Fact]
    public async Task BenchmarkSuite_AllQueries_100PercentAccuracy()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var queries = new Dictionary<string, string>
        {
            { "kem chống nắng cho da dầu", "Kem chống nắng vật lý Múi Xù" },
            { "kem chong nang", "Kem chống nắng vật lý Múi Xù" },
            { "sữa rửa mặt cho da nhờn", "Sữa rửa mặt CeraVe" },
            { "serum vitamin C", "Serum Vitamin C The Ordinary" },
            { "toner cân bằng pH", "Toner Some By Mi AHA BHA PHA" },
            { "kem dưỡng ẩm ban đêm", "Kem dưỡng ẩm Cetaphil" },
            { "mặt nạ ngủ", "Mặt nạ ngủ Laneige" },
            { "tinh chất chống lão hóa", "Tinh chất Estée Lauder Advanced Night Repair" },
            { "kem mắt trà xanh", "Kem mắt Innisfree Green Tea" },
            { "xịt khoáng", "Xịt khoáng Avène" },
            { "dầu tẩy trang", "Dầu tẩy trang DHC" },
            { "kem nền che phủ cao", "Kem nền Maybelline Fit Me" },
            { "son môi lâu trôi", "Son môi MAC Ruby Woo" }
        };

        // Pre-compute product embeddings once
        var productDescriptions = _products.Select(p => p.Description).ToList();
        var productEmbeddings = await _service.EmbedBatchAsync(productDescriptions);

        var totalQueries = queries.Count;
        var correctResults = 0;
        var totalLatency = 0L;

        // Act - Test each query
        foreach (var (query, expectedProduct) in queries)
        {
            var stopwatch = Stopwatch.StartNew();
            var queryEmbedding = await _service.EmbedAsync(query);
            stopwatch.Stop();

            totalLatency += stopwatch.ElapsedMilliseconds;

            var results = _products
                .Select((product, idx) => new
                {
                    Product = product,
                    Similarity = CosineSimilarity(queryEmbedding, productEmbeddings[idx])
                })
                .OrderByDescending(x => x.Similarity)
                .ToList();

            var topResult = results.First();
            if (topResult.Product.Name.Contains(expectedProduct))
            {
                correctResults++;
                _logger.LogInformation(
                    "✓ '{Query}' → '{Product}' (similarity: {Sim:F4})",
                    query, topResult.Product.Name, topResult.Similarity);
            }
            else
            {
                _logger.LogError(
                    "✗ '{Query}' → Expected: '{Expected}', Got: '{Actual}' (similarity: {Sim:F4})",
                    query, expectedProduct, topResult.Product.Name, topResult.Similarity);
            }
        }

        // Assert
        var accuracy = (double)correctResults / totalQueries * 100;
        var avgLatency = totalLatency / totalQueries;

        _logger.LogInformation(
            "Benchmark Results: {Correct}/{Total} correct ({Accuracy:F1}% accuracy), Avg latency: {AvgMs}ms",
            correctResults, totalQueries, accuracy, avgLatency);

        accuracy.Should().Be(100.0,
            "Vietnamese benchmark should achieve 100% accuracy (13/13 queries)");

        avgLatency.Should().BeLessThan(200,
            "Average query latency should be under 200ms");
    }

    [Fact]
    public async Task BatchEmbedding_AllProducts_MeetsPerformanceTarget()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var productDescriptions = _products.Select(p => p.Description).ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var embeddings = await _service.EmbedBatchAsync(productDescriptions);
        stopwatch.Stop();

        // Assert
        embeddings.Should().HaveCount(_products.Count);
        embeddings.Should().OnlyContain(emb => emb.Length == 768);

        var latencyPerProduct = stopwatch.ElapsedMilliseconds / _products.Count;

        _logger.LogInformation(
            "Batch embedded {Count} products in {Ms}ms (avg: {AvgMs}ms/product)",
            _products.Count, stopwatch.ElapsedMilliseconds, latencyPerProduct);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            "Batch embedding should complete within 500ms for 10+ products");
    }

    private List<Product> GetTestProducts()
    {
        return new List<Product>
        {
            new("Kem chống nắng vật lý Múi Xù",
                "Kem chống nắng vật lý Múi Xù SPF50+ PA++++ cho da dầu, da nhạy cảm, không gây bít tắc lỗ chân lông"),
            new("Sữa rửa mặt CeraVe",
                "Sữa rửa mặt CeraVe Foaming Facial Cleanser cho da nhờn, da mụn, làm sạch sâu không gây khô da"),
            new("Serum Vitamin C The Ordinary",
                "Serum Vitamin C The Ordinary 23% + HA Spheres 2% làm sáng da, mờ thâm nám, chống oxy hóa"),
            new("Toner Some By Mi AHA BHA PHA",
                "Toner Some By Mi AHA BHA PHA 30 Days Miracle Toner cân bằng pH, trị mụn, se khít lỗ chân lông"),
            new("Kem dưỡng ẩm Cetaphil",
                "Kem dưỡng ẩm Cetaphil Moisturizing Cream ban đêm cho da khô, da nhạy cảm, cấp ẩm sâu 48h"),
            new("Mặt nạ ngủ Laneige",
                "Mặt nạ ngủ Laneige Water Sleeping Mask cấp ẩm, làm dịu da, phục hồi da qua đêm"),
            new("Tinh chất Estée Lauder Advanced Night Repair",
                "Tinh chất dưỡng da Estée Lauder Advanced Night Repair chống lão hóa, phục hồi da ban đêm"),
            new("Kem mắt Innisfree Green Tea",
                "Kem mắt Innisfree Green Tea Seed Eye Cream giảm quầng thâm, bọng mắt, chống nhăn vùng mắt"),
            new("Xịt khoáng Avène",
                "Xịt khoáng Avène Thermal Spring Water làm dịu da, cấp ẩm, giảm kích ứng cho da nhạy cảm"),
            new("Dầu tẩy trang DHC",
                "Dầu tẩy trang DHC Deep Cleansing Oil làm sạch sâu, tẩy makeup lâu trôi, không gây mụn"),
            new("Kem nền Maybelline Fit Me",
                "Kem nền Maybelline Fit Me Matte + Poreless che phủ cao, kiềm dầu, lâu trôi 12h"),
            new("Son môi MAC Ruby Woo",
                "Son môi MAC Ruby Woo màu đỏ thuần, lâu trôi, không chuyển màu, finish matte"),
            new("Phấn phủ Innisfree No Sebum",
                "Phấn phủ Innisfree No Sebum Mineral Powder kiềm dầu, mờ lỗ chân lông, lâu trôi")
        };
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have same dimensions");
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private record Product(string Name, string Description);
}
