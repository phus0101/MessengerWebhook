using System.Diagnostics;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.IntegrationTests.Services.AI.Embeddings;

/// <summary>
/// Integration tests for VertexAIEmbeddingService using real Vertex AI API
/// Requires valid service account credentials in appsettings.json
/// </summary>
public class VertexAIEmbeddingIntegrationTests : IDisposable
{
    private readonly IEmbeddingService _service;
    private readonly ILogger<VertexAIEmbeddingService> _logger;
    private readonly bool _skipTests;

    public VertexAIEmbeddingIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection("VertexAI").Get<VertexAIOptions>();

        // Skip tests if credentials not configured
        if (options == null || string.IsNullOrEmpty(options.ServiceAccountKeyPath) ||
            !File.Exists(options.ServiceAccountKeyPath))
        {
            _skipTests = true;
            _service = null!;
            _logger = null!;
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
    }

    [Fact]
    public async Task EmbedAsync_VietnameseText_Returns768Dimensions()
    {
        if (_skipTests)
        {
            // Skip test if credentials not configured
            return;
        }

        // Arrange
        var text = "Kem chống nắng vật lý Múi Xù SPF50+ PA++++";

        // Act
        var embedding = await _service.EmbedAsync(text);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768, "text-embedding-004 produces 768-dimensional vectors");
        embedding.Should().NotContain(0f, "embedding should not be all zeros");
        embedding.Should().OnlyContain(v => v >= -1.0f && v <= 1.0f, "embedding values should be normalized");
    }

    [Fact]
    public async Task EmbedAsync_DiacriticsVsNoDiacritics_AreSimilar()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var withDiacritics = "Kem chống nắng cho da dầu";
        var withoutDiacritics = "Kem chong nang cho da dau";

        // Act
        var emb1 = await _service.EmbedAsync(withDiacritics);
        var emb2 = await _service.EmbedAsync(withoutDiacritics);

        // Assert
        var similarity = CosineSimilarity(emb1, emb2);
        similarity.Should().BeGreaterThan(0.6,
            "embeddings with/without diacritics should be semantically similar");

        _logger.LogInformation(
            "Diacritics similarity: {Similarity:F4} (with: '{With}', without: '{Without}')",
            similarity, withDiacritics, withoutDiacritics);
    }

    [Fact]
    public async Task EmbedBatchAsync_MultipleTexts_ReturnsCorrectCount()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var texts = new List<string>
        {
            "Kem chống nắng vật lý",
            "Sữa rửa mặt cho da nhờn",
            "Serum vitamin C làm sáng da",
            "Toner cân bằng độ pH",
            "Kem dưỡng ẩm ban đêm"
        };

        // Act
        var embeddings = await _service.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().HaveCount(texts.Count);
        embeddings.Should().OnlyContain(emb => emb.Length == 768);
        embeddings.Should().OnlyContain(emb => emb.Any(v => v != 0f));
    }

    [Fact]
    public async Task EmbedBatchAsync_10Products_MeetsLatencyRequirement()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var texts = new List<string>
        {
            "Kem chống nắng vật lý Múi Xù SPF50+",
            "Sữa rửa mặt CeraVe cho da nhờn",
            "Serum Vitamin C The Ordinary 23%",
            "Toner Some By Mi AHA BHA PHA",
            "Kem dưỡng ẩm Cetaphil ban đêm",
            "Mặt nạ ngủ Laneige Water Sleeping Mask",
            "Tinh chất dưỡng da Estée Lauder Advanced Night Repair",
            "Kem mắt Innisfree Green Tea Seed Eye Cream",
            "Xịt khoáng Avène Thermal Spring Water",
            "Dầu tẩy trang DHC Deep Cleansing Oil"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var embeddings = await _service.EmbedBatchAsync(texts);
        stopwatch.Stop();

        // Assert
        embeddings.Should().HaveCount(10);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
            "batch embedding for 10 products should complete within 500ms");

        _logger.LogInformation(
            "Batch embedding latency: {Ms}ms for {Count} products (avg: {AvgMs}ms/product)",
            stopwatch.ElapsedMilliseconds,
            texts.Count,
            stopwatch.ElapsedMilliseconds / texts.Count);
    }

    [Fact]
    public async Task EmbedAsync_SingleProduct_MeetsLatencyRequirement()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var text = "Kem chống nắng vật lý Múi Xù SPF50+ PA++++";

        // Act
        var stopwatch = Stopwatch.StartNew();
        var embedding = await _service.EmbedAsync(text);
        stopwatch.Stop();

        // Assert
        embedding.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200,
            "single embedding should complete within 200ms");

        _logger.LogInformation(
            "Single embedding latency: {Ms}ms",
            stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task EmbedAsync_SpecialCharacters_HandlesCorrectly()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var texts = new List<string>
        {
            "Kem chống nắng SPF50+ PA++++",
            "Serum 10% Niacinamide + 1% Zinc",
            "Toner pH 5.5 (cân bằng da)",
            "Mặt nạ 24K Gold & Collagen"
        };

        // Act
        var embeddings = await _service.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().HaveCount(texts.Count);
        embeddings.Should().OnlyContain(emb => emb.Length == 768);
    }

    [Fact]
    public async Task EmbedAsync_LongText_HandlesCorrectly()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var longText = @"Kem chống nắng vật lý Múi Xù SPF50+ PA++++ là sản phẩm chống nắng
            hoàn toàn từ khoáng chất, phù hợp cho da nhạy cảm, da mụn. Công thức không chứa
            hóa chất gây kích ứng, không gây bít tắc lỗ chân lông. Kết cấu mỏng nhẹ,
            thẩm thấu nhanh, không để lại vệt trắng. Chỉ số chống nắng cao SPF50+ PA++++
            bảo vệ da khỏi tia UVA và UVB hiệu quả suốt cả ngày.";

        // Act
        var embedding = await _service.EmbedAsync(longText);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768);
    }

    [Fact]
    public async Task EmbedAsync_EmptyString_ThrowsOrReturnsValidEmbedding()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var emptyText = "";

        // Act & Assert
        // Vertex AI may handle empty strings differently
        // Either it throws an exception or returns a valid embedding
        try
        {
            var embedding = await _service.EmbedAsync(emptyText);
            embedding.Should().NotBeNull();
            embedding.Length.Should().Be(768);
        }
        catch (Exception ex)
        {
            // Expected behavior - empty text may not be supported
            var isExpectedType = ex is HttpRequestException || ex is InvalidOperationException;
            isExpectedType.Should().BeTrue("Empty text should throw HttpRequestException or InvalidOperationException");
        }
    }

    [Fact]
    public async Task EmbedBatchAsync_SimilarProducts_HaveHighSimilarity()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var product1 = "Kem chống nắng vật lý cho da nhạy cảm";
        var product2 = "Kem chống nắng khoáng chất cho da mụn";
        var product3 = "Sữa rửa mặt tạo bọt cho da dầu"; // Different category

        // Act
        var embeddings = await _service.EmbedBatchAsync(new List<string>
        {
            product1, product2, product3
        });

        // Assert
        var sim12 = CosineSimilarity(embeddings[0], embeddings[1]);
        var sim13 = CosineSimilarity(embeddings[0], embeddings[2]);

        sim12.Should().BeGreaterThan(sim13,
            "similar products (sunscreens) should have higher similarity than different categories");

        _logger.LogInformation(
            "Similarity - Sunscreen vs Sunscreen: {Sim12:F4}, Sunscreen vs Cleanser: {Sim13:F4}",
            sim12, sim13);
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
}
