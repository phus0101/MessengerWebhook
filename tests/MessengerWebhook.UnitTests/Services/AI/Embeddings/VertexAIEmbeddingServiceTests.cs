using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace MessengerWebhook.UnitTests.Services.AI.Embeddings;

/// <summary>
/// Unit tests for VertexAIEmbeddingService
/// Note: These tests mock HTTP responses and skip real authentication
/// </summary>
public class VertexAIEmbeddingServiceTests
{
    private readonly Mock<ILogger<VertexAIEmbeddingService>> _loggerMock;

    public VertexAIEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<VertexAIEmbeddingService>>();
    }

    [Fact]
    public async Task EmbedAsync_ValidText_Returns768Dimensions()
    {
        // Arrange
        var mockResponse = CreateMockResponse(1);
        var service = CreateMockService(mockResponse);

        // Act
        var embedding = await service.EmbedAsync("Kem chống nắng cho da dầu");

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768);
        embedding.Should().OnlyContain(v => v >= -1.0f && v <= 1.0f);
    }

    [Fact]
    public async Task EmbedBatchAsync_MultipleTexts_ReturnsCorrectCount()
    {
        // Arrange
        var texts = new List<string>
        {
            "Kem chống nắng",
            "Sữa rửa mặt",
            "Serum vitamin C"
        };
        var mockResponse = CreateMockResponse(texts.Count);
        var service = CreateMockService(mockResponse);

        // Act
        var embeddings = await service.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().HaveCount(3);
        embeddings.Should().OnlyContain(emb => emb.Length == 768);
    }

    [Fact]
    public async Task EmbedBatchAsync_Over100Texts_ThrowsArgumentException()
    {
        // Arrange
        var texts = Enumerable.Range(0, 101)
            .Select(i => $"Product {i}")
            .ToList();
        var service = CreateMockService(CreateMockResponse(1));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.EmbedBatchAsync(texts));

        exception.Message.Should().Contain("max 100 texts");
        exception.ParamName.Should().Be("texts");
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var mockResponse = CreateMockResponse(0);
        var service = CreateMockService(mockResponse);

        // Act
        var embeddings = await service.EmbedBatchAsync(new List<string>());

        // Assert
        embeddings.Should().BeEmpty();
    }

    [Fact]
    public async Task EmbedAsync_ApiError_ThrowsHttpRequestException()
    {
        // Arrange
        var service = CreateMockService(
            HttpStatusCode.InternalServerError,
            "{\"error\": \"Internal server error\"}");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.EmbedAsync("test"));

        exception.Message.Should().Contain("Vertex AI API error");
        exception.Message.Should().Contain("500");
    }

    [Fact]
    public async Task EmbedAsync_EmptyPredictions_ThrowsInvalidOperationException()
    {
        // Arrange
        var emptyResponse = new
        {
            predictions = Array.Empty<object>()
        };
        var service = CreateMockService(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(emptyResponse));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EmbedAsync("test"));

        exception.Message.Should().Contain("empty predictions");
    }

    [Fact]
    public async Task EmbedAsync_VietnameseText_HandlesCorrectly()
    {
        // Arrange
        var mockResponse = CreateMockResponse(1);
        var service = CreateMockService(mockResponse);
        var vietnameseText = "Kem chống nắng vật lý Múi Xù SPF50+ PA++++";

        // Act
        var embedding = await service.EmbedAsync(vietnameseText);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Length.Should().Be(768);
    }

    [Fact]
    public async Task EmbedBatchAsync_MaxBatchSize_Succeeds()
    {
        // Arrange
        var texts = Enumerable.Range(0, 100)
            .Select(i => $"Product {i}")
            .ToList();
        var mockResponse = CreateMockResponse(100);
        var service = CreateMockService(mockResponse);

        // Act
        var embeddings = await service.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().HaveCount(100);
        embeddings.Should().OnlyContain(emb => emb.Length == 768);
    }

    [Fact]
    public async Task EmbedAsync_LogsLatencyMetrics()
    {
        // Arrange
        var mockResponse = CreateMockResponse(1);
        var service = CreateMockService(mockResponse);

        // Act
        await service.EmbedAsync("test");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("embeddings in")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task EmbedAsync_ApiError_LogsError()
    {
        // Arrange
        var service = CreateMockService(
            HttpStatusCode.BadRequest,
            "{\"error\": \"Invalid request\"}");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.EmbedAsync("test"));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Vertex AI API error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private TestableVertexAIEmbeddingService CreateMockService(string responseContent)
    {
        return CreateMockService(HttpStatusCode.OK, responseContent);
    }

    private TestableVertexAIEmbeddingService CreateMockService(
        HttpStatusCode statusCode,
        string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var options = Options.Create(new VertexAIOptions
        {
            ProjectId = "test-project",
            Location = "asia-southeast1",
            Model = "text-embedding-004"
        });

        return new TestableVertexAIEmbeddingService(httpClient, options, _loggerMock.Object);
    }

    private string CreateMockResponse(int count)
    {
        var predictions = Enumerable.Range(0, count)
            .Select(_ => new
            {
                embeddings = new
                {
                    values = Enumerable.Range(0, 768)
                        .Select(i => (float)(Math.Sin(i) * 0.5))
                        .ToArray()
                }
            })
            .ToArray();

        var response = new { predictions };
        return JsonSerializer.Serialize(response);
    }

    /// <summary>
    /// Testable version that skips authentication initialization
    /// </summary>
    private class TestableVertexAIEmbeddingService : VertexAIEmbeddingService
    {
        public TestableVertexAIEmbeddingService(
            HttpClient httpClient,
            IOptions<VertexAIOptions> options,
            ILogger<VertexAIEmbeddingService> logger)
            : base(httpClient, options, logger)
        {
            // Skip authentication for unit tests
        }

        // Override to skip authentication
        protected override void InitializeAuthentication()
        {
            // No-op for unit tests
        }

        protected override Task<string> GetAccessTokenAsync()
        {
            // Return mock token for unit tests
            return Task.FromResult("mock-access-token");
        }
    }
}
