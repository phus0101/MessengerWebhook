using System.Net;
using System.Text.Json;
using FluentAssertions;
using MessengerWebhook.Configuration;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MessengerWebhook.UnitTests.Services.AI;

/// <summary>
/// Unit tests for GeminiEmbeddingService
/// </summary>
public class GeminiEmbeddingServiceTests
{
    private readonly Mock<ILogger<GeminiEmbeddingService>> _loggerMock;
    private readonly IOptions<GeminiOptions> _options;

    public GeminiEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<GeminiEmbeddingService>>();
        _options = Options.Create(new GeminiOptions
        {
            ApiKey = "test_api_key",
            TimeoutSeconds = 30
        });
    }

    private GeminiEmbeddingService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
        return new GeminiEmbeddingService(httpClient, _options, _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateAsync_ValidText_Returns768DimensionEmbedding()
    {
        // Arrange
        var embedding = new float[768];
        for (int i = 0; i < 768; i++) embedding[i] = 0.1f;

        var response = new EmbeddingResponse
        {
            Embedding = new EmbeddingData { Values = embedding }
        };
        var jsonResponse = JsonSerializer.Serialize(response);
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(jsonResponse);
        var service = CreateService(handler);

        // Act
        var result = await service.GenerateAsync("test product description");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(768);
        result.Should().AllSatisfy(v => v.Should().Be(0.1f));
    }

    [Fact]
    public async Task GenerateAsync_EmptyText_ThrowsArgumentException()
    {
        // Arrange
        var handler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var service = CreateService(handler);

        // Act
        var act = async () => await service.GenerateAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    [Fact]
    public async Task GenerateAsync_NullText_ThrowsArgumentException()
    {
        // Arrange
        var handler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var service = CreateService(handler);

        // Act
        var act = async () => await service.GenerateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("text");
    }

    [Fact]
    public async Task GenerateAsync_ApiError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = MockHttpMessageHandler.CreateWithError(
            HttpStatusCode.BadRequest,
            "Invalid API key");
        var service = CreateService(handler);

        // Act
        var act = async () => await service.GenerateAsync("test text");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*BadRequest*");
    }

    [Fact]
    public async Task GenerateAsync_EmptyResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var response = new EmbeddingResponse
        {
            Embedding = new EmbeddingData { Values = Array.Empty<float>() }
        };
        var jsonResponse = JsonSerializer.Serialize(response);
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(jsonResponse);
        var service = CreateService(handler);

        // Act
        var act = async () => await service.GenerateAsync("test text");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty result*");
    }

    [Fact]
    public async Task GenerateBatchAsync_MultipleTexts_ReturnsCorrectCount()
    {
        // Arrange
        var embedding1 = new float[768];
        var embedding2 = new float[768];
        for (int i = 0; i < 768; i++)
        {
            embedding1[i] = 0.1f;
            embedding2[i] = 0.2f;
        }

        var response = new BatchEmbeddingResponse
        {
            Embeddings = new List<EmbeddingResponse>
            {
                new() { Embedding = new EmbeddingData { Values = embedding1 } },
                new() { Embedding = new EmbeddingData { Values = embedding2 } }
            }
        };
        var jsonResponse = JsonSerializer.Serialize(response);
        var handler = MockHttpMessageHandler.CreateWithJsonResponse(jsonResponse);
        var service = CreateService(handler);

        var texts = new List<string> { "text 1", "text 2" };

        // Act
        var result = await service.GenerateBatchAsync(texts);

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().HaveCount(768);
        result[1].Should().HaveCount(768);
        result[0].Should().AllSatisfy(v => v.Should().Be(0.1f));
        result[1].Should().AllSatisfy(v => v.Should().Be(0.2f));
    }

    [Fact]
    public async Task GenerateBatchAsync_Over100Texts_BatchesCorrectly()
    {
        // Arrange
        var embedding = new float[768];
        for (int i = 0; i < 768; i++) embedding[i] = 0.1f;

        // Create 150 texts (should be split into 2 batches: 100 + 50)
        var texts = Enumerable.Range(1, 150).Select(i => $"text {i}").ToList();

        var callCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            callCount++;
            var expectedCount = callCount == 1 ? 100 : 50;

            var embeddings = Enumerable.Range(0, expectedCount)
                .Select(_ => new EmbeddingResponse
                {
                    Embedding = new EmbeddingData { Values = embedding }
                })
                .ToList();

            var response = new BatchEmbeddingResponse { Embeddings = embeddings };
            var jsonResponse = JsonSerializer.Serialize(response);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateBatchAsync(texts);

        // Assert
        result.Should().HaveCount(150);
        result.Should().AllSatisfy(emb => emb.Should().HaveCount(768));
    }

    [Fact]
    public async Task GenerateBatchAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var handler = MockHttpMessageHandler.CreateWithJsonResponse("{}");
        var service = CreateService(handler);

        // Act
        var result = await service.GenerateBatchAsync(new List<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateBatchAsync_ApiError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = MockHttpMessageHandler.CreateWithError(
            HttpStatusCode.InternalServerError,
            "Server error");
        var service = CreateService(handler);

        var texts = new List<string> { "text 1", "text 2" };

        // Act
        var act = async () => await service.GenerateBatchAsync(texts);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*InternalServerError*");
    }
}
